using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace TrackingRecorder.Core;

public readonly record struct Sample(long TicksUtc, double MonotonicMs, Timecode Tc, FreeDPose Pose, byte[] Raw);

/// <summary>
/// Empfängt FreeD per UDP in einem eigenen Thread mit hoher Priorität.
/// Kein UI, kein Logging im Empfangspfad — nur Zeitstempel setzen und weiterreichen.
/// </summary>
public sealed class FreeDReceiver : IDisposable
{
    readonly UdpClient _udp;
    readonly ITimecodeProvider _tc;
    readonly Thread _thread;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    volatile bool _running = true;

    public event Action<Sample>? SampleReceived;
    public long Received, Invalid;

    public FreeDReceiver(int port, ITimecodeProvider tc)
    {
        _tc = tc;
        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.ReceiveBufferSize = 1 << 20;
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        _thread = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "FreeD-Empfang" };
        _thread.Start();
    }

    void Loop()
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            byte[] data;
            try { data = _udp.Receive(ref remote); }
            catch (SocketException) { if (!_running) return; continue; }
            catch (ObjectDisposedException) { return; }

            double mono = _clock.Elapsed.TotalMilliseconds;
            var tc = _tc.Now();
            long now = DateTime.UtcNow.Ticks;
            if (FreeD.TryParse(data, out var pose))
            {
                Interlocked.Increment(ref Received);
                SampleReceived?.Invoke(new Sample(now, mono, tc, pose, data));
            }
            else Interlocked.Increment(ref Invalid);
        }
    }

    public void Dispose() { _running = false; _udp.Dispose(); }
}

/// <summary>
/// Schreibt Proben sofort als CSV — Datei wird nur erweitert und regelmässig geflusht,
/// damit bei einem Absturz die Aufnahme bis kurz davor erhalten bleibt.
/// Das Rohpaket (hex) wird mitgeschrieben, damit nichts verloren geht, was wir heute noch nicht verstehen.
/// </summary>
public sealed class CsvRecorder : IDisposable
{
    public const string Header =
        "utc_iso,mono_ms,timecode,fps,tc_source,camera_id,pan_deg,tilt_deg,roll_deg,x_mm,y_mm,z_mm,zoom,focus,spare,raw_hex";

    readonly StreamWriter _w;
    readonly TimecodeSource _source;
    readonly object _lock = new();
    long _lastFlush;

    public CsvRecorder(string path, TimecodeSource source)
    {
        _source = source;
        _w = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        _w.WriteLine(Header);
    }

    public void Write(in Sample s)
    {
        var c = CultureInfo.InvariantCulture;
        var p = s.Pose;
        string line = string.Join(',',
            new DateTime(s.TicksUtc, DateTimeKind.Utc).ToString("O", c),
            s.MonotonicMs.ToString("F3", c),
            s.Tc.ToString(), s.Tc.Fps.ToString(c), _source,
            p.CameraId, p.PanDeg.ToString("F6", c), p.TiltDeg.ToString("F6", c), p.RollDeg.ToString("F6", c),
            p.XMm.ToString("F4", c), p.YMm.ToString("F4", c), p.ZMm.ToString("F4", c),
            p.Zoom, p.Focus, p.Spare, Convert.ToHexString(s.Raw));
        lock (_lock)
        {
            _w.WriteLine(line);
            if (s.MonotonicMs - _lastFlush > 200) { _w.Flush(); _lastFlush = (long)s.MonotonicMs; }
        }
    }

    public void Dispose() { lock (_lock) _w.Dispose(); }
}
