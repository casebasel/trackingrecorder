using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TrackingRecorder.Core;

/// <summary>Ein Gerät aus einem Mars-Datenbild: Rover-Tracker (LHR-…) oder Basisstation (LHB-…).</summary>
public readonly record struct MarsDevice(
    string Serial,
    double X, double Y, double Z,        // Meter, Y oben (wie Path Recorder)
    double Qx, double Qy, double Qz, double Qw)
{
    public bool IsBaseStation => Serial.StartsWith("LHB", StringComparison.Ordinal);
}

/// <summary>Ein Bild aus dem Mars-Datenstrom (UDP 5500, der Weg von Path Recorder).</summary>
public sealed record MarsFrame(double DeviceTimeMs, Timecode Tc, float Fps, IReadOnlyList<MarsDevice> Devices);

/// <summary>
/// Protokoll von Mars-Port 5500, entschlüsselt aus einem Mitschnitt von Path Recorder (10.11.2026):
/// Anfrage 1 Byte: **0x06 = anmelden, 0x07 = abmelden** (aus dem Code von Path Recorder, UDPListener:
/// beim Verbinden 07 + 06, beim Beenden 07; am 10.11.2026 am Gerät bestätigt). Mars sendet jedem angemeldeten
/// Absender (IP + Port) weiter, bis er sich mit 0x07 abmeldet — auch wenn das Programm längst beendet ist. Antwort pro Bild:
///   0–12   fester Kopf 00 01 00 00 00 00 00 04 00 02 00 05 00
///   13–20  double  Geräte-Zeit in ms (40 ms Schritt bei 25p, Uhr der Box, nicht Tageszeit)
///   21     Bildnummer (= Timecode-Frame)
///   22–25  float   Bildrate
///   26–29  Timecode h, m, s, f
///   30     Anzahl Geräte n
///   31…    n × 91 Bytes: [0] Index, [1–8] Bild-Zeitstempel, [9–20] Seriennummer ASCII,
///                       [21] 0, [22–77] 7 × double: Position x,y,z (m), Quaternion x,y,z,w, [78–90] 0
///   Ende   2 Bytes (vermutlich Prüfsumme, nicht ausgewertet)
/// </summary>
public static class MarsProtocol
{
    public const int Port = 5500;
    public const byte Subscribe = 0x06, Unsubscribe = 0x07;
    const int HeaderLength = 31, RecordLength = 91;

    public static bool TryParse(ReadOnlySpan<byte> d, out MarsFrame? frame)
    {
        frame = null;
        if (d.Length < HeaderLength || d[0] != 0x00 || d[1] != 0x01) return false;
        int n = d[30];
        if (d.Length < HeaderLength + n * RecordLength) return false;

        double deviceMs = BinaryPrimitives.ReadDoubleLittleEndian(d[13..]);
        float fps = BinaryPrimitives.ReadSingleLittleEndian(d[22..]);
        var tc = new Timecode(d[26], d[27], d[28], d[29], fps);

        var devices = new MarsDevice[n];
        for (int k = 0; k < n; k++)
        {
            var r = d.Slice(HeaderLength + k * RecordLength, RecordLength);
            string serial = Encoding.ASCII.GetString(r.Slice(9, 12)).TrimEnd('\0');
            devices[k] = new MarsDevice(serial, V(r, 0), V(r, 1), V(r, 2), V(r, 3), V(r, 4), V(r, 5), V(r, 6));
        }
        frame = new MarsFrame(deviceMs, tc, fps, devices);
        return true;
    }

    static double V(ReadOnlySpan<byte> record, int i) => BinaryPrimitives.ReadDoubleLittleEndian(record[(22 + i * 8)..]);
}

public readonly record struct MarsSample(long TicksUtc, double MonotonicMs, MarsFrame Frame, byte[] Raw);

/// <summary>
/// Holt den Mars-Datenstrom ab wie Path Recorder: 0x07 (alte Anmeldung lösen) + 0x06 (anmelden), erneut, wenn
/// 3 s lang nichts kommt. Beim Beenden 0x07, damit Mars nicht ins Leere weitersendet. Fester lokaler Port:
/// Wird die App hart beendet, trifft der nächste Start dieselbe Anmeldung statt eine weitere anzulegen.
/// Empfang in eigenem Thread mit hoher Priorität, ohne UI und ohne Logging im Empfangspfad.
/// </summary>
public sealed class MarsClient : IDisposable
{
    readonly UdpClient _udp;
    readonly IPEndPoint _mars;
    readonly Thread _thread;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    volatile bool _running = true;
    double _lastPacketMs = -10_000;

    public event Action<MarsSample>? FrameReceived;
    public long Received, Invalid, Requests;
    public int LocalPort => ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;

    public const int DefaultLocalPort = 55500;

    public MarsClient(IPAddress mars, int localPort = DefaultLocalPort)
    {
        _mars = new IPEndPoint(mars, MarsProtocol.Port);
        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, localPort));
        // Windows: ICMP „Port unreachable" sonst als Fehler beim nächsten Receive (WSAECONNRESET)
        if (OperatingSystem.IsWindows()) _udp.Client.IOControl(-1744830452 /* SIO_UDP_CONNRESET */, [0], null);
        _udp.Client.ReceiveBufferSize = 4 << 20;
        _udp.Client.ReceiveTimeout = 500;
        _thread = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "Mars-Empfang" };
        _thread.Start();
    }

    void Request(byte b)
    {
        try { _udp.Send([b], 1, _mars); Interlocked.Increment(ref Requests); } catch (SocketException) { }
    }

    void Loop()
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        double lastRequest = -10_000;
        while (_running)
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            if (now - _lastPacketMs > 3000 && now - lastRequest > 3000) { Request(MarsProtocol.Unsubscribe); Request(MarsProtocol.Subscribe); lastRequest = now; }

            byte[] data;
            try { data = _udp.Receive(ref remote); }
            catch (SocketException) { continue; }
            catch (ObjectDisposedException) { return; }
            if (!_running || data.Length == 0) continue;

            double mono = _clock.Elapsed.TotalMilliseconds;
            long ticks = DateTime.UtcNow.Ticks;
            _lastPacketMs = mono;
            if (MarsProtocol.TryParse(data, out var frame))
            {
                Interlocked.Increment(ref Received);
                FrameReceived?.Invoke(new MarsSample(ticks, mono, frame!, data));
            }
            else Interlocked.Increment(ref Invalid);
        }
    }

    public void Dispose()
    {
        _running = false;
        Request(MarsProtocol.Unsubscribe);
        _udp.Dispose();
    }
}
