using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace TrackingRecorder.Cli;

/// <summary>
/// Rohmitschnitt zur Analyse: FreeD (UDP 6001) und Live Link (Unreal Message Bus, Multicast 230.0.0.1:6666).
/// Jedes Paket landet unverändert als Zeile „zeit_utc;mono_ms;kanal;absender;länge;hex" in einer Datei.
/// Optional werden die Pakete per Unicast weitergeleitet (z. B. an den Server-Claude zur Live-Analyse).
/// </summary>
static class Mitschnitt
{
    static volatile bool _running = true;

    public static int Run(string[] args)
    {
        int freedPort = 6001, llPort = 6666;
        var llGroup = IPAddress.Parse("230.0.0.1");
        IPEndPoint? relay = null;
        string outPath = $"mitschnitt_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt";
        for (int i = 1; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--freed-port": freedPort = int.Parse(args[++i]); break;
                case "--ll-port": llPort = int.Parse(args[++i]); break;
                case "--ll-group": llGroup = IPAddress.Parse(args[++i]); break;
                case "--out": outPath = args[++i]; break;
                case "--relay": relay = IPEndPoint.Parse(args[++i]); break;
            }
        }

        var clock = Stopwatch.StartNew();
        using var writer = new StreamWriter(new FileStream(outPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), Encoding.ASCII);
        writer.WriteLine("# TrackingRecorder Mitschnitt · zeit_utc;mono_ms;kanal;absender;laenge;hex");
        var lockObj = new object();
        var counts = new Dictionary<string, long>();
        using var relaySock = relay is null ? null : new UdpClient();

        void Handle(string kanal, byte[] data, IPEndPoint from)
        {
            double mono = clock.Elapsed.TotalMilliseconds;
            string line = $"{DateTime.UtcNow:O};{mono:F3};{kanal};{from};{data.Length};{Convert.ToHexString(data)}";
            lock (lockObj)
            {
                writer.WriteLine(line);
                counts[kanal] = counts.GetValueOrDefault(kanal) + 1;
            }
            if (relaySock is not null)
            {
                // Kanal als 1. Byte voranstellen: 1 = FreeD, 2 = Live Link
                var buf = new byte[data.Length + 1];
                buf[0] = kanal == "freed" ? (byte)1 : (byte)2;
                data.CopyTo(buf, 1);
                try { relaySock.Send(buf, buf.Length, relay); } catch (SocketException) { }
            }
        }

        var freed = Open(freedPort, null);
        var ll = Open(llPort, llGroup);
        StartLoop("freed", freed, Handle);
        StartLoop("livelink", ll, Handle);

        var stop = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        Console.WriteLine($"Mitschnitt → {Path.GetFullPath(outPath)}");
        Console.WriteLine($"FreeD UDP {freedPort} · Live Link {llGroup}:{llPort}" + (relay is null ? "" : $" · Weiterleitung an {relay}"));
        Console.WriteLine("Ctrl+C beendet.");

        var prev = new Dictionary<string, long>();
        while (!stop.Wait(1000))
        {
            lock (lockObj)
            {
                writer.Flush();
                var parts = new[] { "freed", "livelink" }.Select(k =>
                {
                    long n = counts.GetValueOrDefault(k);
                    long rate = n - prev.GetValueOrDefault(k);
                    prev[k] = n;
                    return $"{k}: {rate,4}/s ({n} gesamt)";
                });
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] " + string.Join("   ", parts));
            }
        }
        _running = false;
        freed.Dispose();
        ll.Dispose();
        Console.WriteLine($"Fertig. Datei: {Path.GetFullPath(outPath)}");
        return 0;
    }

    static UdpClient Open(int port, IPAddress? group)
    {
        var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.ReceiveBufferSize = 4 << 20;
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        if (group is not null)
        {
            // Unter Windows pro Netzwerkkarte beitreten, sonst landet der Beitritt evtl. auf der falschen.
            foreach (var addr in LocalIPv4())
            {
                try
                {
                    udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(group, addr));
                    Console.WriteLine($"  Multicast {group} beigetreten auf {addr}");
                }
                catch (SocketException e) { Console.WriteLine($"  Multicast auf {addr} nicht möglich: {e.Message}"); }
            }
        }
        return udp;
    }

    static IEnumerable<IPAddress> LocalIPv4() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address);

    static void StartLoop(string kanal, UdpClient udp, Action<string, byte[], IPEndPoint> handle)
    {
        new Thread(() =>
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                byte[] data;
                try { data = udp.Receive(ref from); }
                catch (SocketException) { continue; }
                catch (ObjectDisposedException) { return; }
                if (_running && data.Length > 0) handle(kanal, data, from);
            }
        }) { IsBackground = true, Priority = ThreadPriority.Highest, Name = kanal }.Start();
    }
}
