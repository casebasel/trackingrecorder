using System.Globalization;
using System.Net;
using System.Net.Sockets;
using TrackingRecorder.Core;

namespace TrackingRecorder.Cli;

/// <summary>
/// Aufnahme über den Mars-Datenstrom (UDP 5500, wie Path Recorder): Timecode, alle Tracker und Basisstationen.
///   TrackingRecorder mars [--ip 192.168.100.60] [--out take.csv] [--relay 10.10.10.70:7002]
/// </summary>
static class MarsModus
{
    public static int Run(string[] args)
    {
        var ip = IPAddress.Parse("192.168.100.60");
        string outPath = $"mars_{DateTime.Now:yyyy-MM-dd_HHmmss}.csv";
        IPEndPoint? relay = null;
        for (int i = 1; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--ip": ip = IPAddress.Parse(args[++i]); break;
                case "--out": outPath = args[++i]; break;
                case "--relay": relay = IPEndPoint.Parse(args[++i]); break;
            }
        }

        using var recorder = new MarsRecorder(outPath);
        var monitor = new MarsMonitor();
        using var relaySock = relay is null ? null : new UdpClient();
        MarsFrame? latest = null;
        using var client = new MarsClient(ip);
        client.FrameReceived += s =>
        {
            recorder.Write(s);
            lock (monitor) monitor.Add(s.Frame);
            latest = s.Frame;
            if (relaySock is not null) try { relaySock.Send(s.Raw, s.Raw.Length, relay); } catch (SocketException) { }
        };

        var stop = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        Console.WriteLine($"Mars {ip}:{MarsProtocol.Port} · Aufnahme → {Path.GetFullPath(outPath)}" +
                          (relay is null ? "" : $" · Weiterleitung an {relay}") + " · Ctrl+C beendet");

        long prevFrames = 0;
        int shownEvents = 0;
        while (!stop.Wait(1000))
        {
            var f = latest;
            long frames;
            lock (monitor) frames = monitor.Frames;
            if (f is null)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] warte auf Mars … (Anfragen: {client.Requests})");
                continue;
            }
            Console.WriteLine(FormattableString.Invariant(
                $"[{f.Tc}] {frames - prevFrames,3} Bilder/s · fehlend {monitor.MissingFrames} · Sprünge {monitor.Jumps} · Kartensprünge {monitor.BaseShifts}"));
            prevFrames = frames;
            foreach (var d in f.Devices)
            {
                string extra = d.IsBaseStation ? FormattableString.Invariant($"  Drift {monitor.BaseStationDriftMm(d.Serial),6:F1} mm") : "";
                Console.WriteLine(FormattableString.Invariant(
                    $"   {(d.IsBaseStation ? "Basis  " : "Tracker")} {d.Serial,-13} X {d.X,8:F4} Y {d.Y,8:F4} Z {d.Z,8:F4} m{extra}"));
            }
            lock (monitor.Events)
                for (; shownEvents < monitor.Events.Count; shownEvents++)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("   ⚠ " + monitor.Events[shownEvents]);
                    Console.ResetColor();
                }
        }
        Console.WriteLine($"Fertig. {client.Received} Bilder · Datei: {Path.GetFullPath(outPath)}");
        return 0;
    }
}
