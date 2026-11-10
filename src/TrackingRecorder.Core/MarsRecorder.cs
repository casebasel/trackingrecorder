using System.Globalization;

namespace TrackingRecorder.Core;

/// <summary>
/// Schreibt den Mars-Strom roh mit: eine CSV-Zeile pro Gerät und Bild, dazu eine Rohdatei (hex, eine Zeile pro Paket),
/// damit auch Felder erhalten bleiben, die wir heute noch nicht auswerten. Nur anhängend, regelmässig geflusht.
/// </summary>
public sealed class MarsRecorder : IDisposable
{
    public const string Header =
        "utc_iso,mono_ms,device_ms,timecode,fps,serial,kind,x_m,y_m,z_m,qx,qy,qz,qw";

    readonly StreamWriter _csv, _raw;
    readonly object _lock = new();
    double _lastFlush;

    public MarsRecorder(string csvPath)
    {
        _csv = Open(csvPath);
        _raw = Open(Path.ChangeExtension(csvPath, ".raw.txt"));
        _csv.WriteLine(Header);
        _raw.WriteLine("# utc_iso;mono_ms;hex — Mars UDP 5500, ein Paket pro Zeile");
    }

    static StreamWriter Open(string p) => new(new FileStream(p, FileMode.CreateNew, FileAccess.Write, FileShare.Read));

    public void Write(in MarsSample s)
    {
        var c = CultureInfo.InvariantCulture;
        string utc = new DateTime(s.TicksUtc, DateTimeKind.Utc).ToString("O", c);
        string mono = s.MonotonicMs.ToString("F3", c);
        var f = s.Frame;
        lock (_lock)
        {
            foreach (var d in f.Devices)
            {
                _csv.WriteLine(string.Join(',', utc, mono, f.DeviceTimeMs.ToString("F3", c), f.Tc.ToString(),
                    f.Fps.ToString(c), d.Serial, d.IsBaseStation ? "base" : "tracker",
                    d.X.ToString("R", c), d.Y.ToString("R", c), d.Z.ToString("R", c),
                    d.Qx.ToString("R", c), d.Qy.ToString("R", c), d.Qz.ToString("R", c), d.Qw.ToString("R", c)));
            }
            _raw.WriteLine($"{utc};{mono};{Convert.ToHexString(s.Raw)}");
            if (s.MonotonicMs - _lastFlush > 200) { _csv.Flush(); _raw.Flush(); _lastFlush = s.MonotonicMs; }
        }
    }

    public void Dispose() { lock (_lock) { _csv.Dispose(); _raw.Dispose(); } }
}

/// <summary>
/// Überwacht die Qualität live: fehlende Bilder (Timecode/Geräte-Zeit), Sprünge der Tracker,
/// und „wandernde" Basisstationen. Die Stationen hängen fest — jede Bewegung ist ein Schätzfehler von Mars
/// und verschiebt das ganze Koordinatensystem.
/// </summary>
public sealed class MarsMonitor
{
    public double JumpLimitM = 0.10;        // mehr als 10 cm in einem Bild = Ausreisser
    public double BaseShiftLimitM = 0.005;  // Basisstation mehr als 5 mm weg vom Start = Kartensprung

    readonly Dictionary<string, MarsDevice> _first = new(), _last = new();
    double _lastDeviceMs = double.NaN;

    public long Frames, MissingFrames, Jumps, BaseShifts;
    public readonly List<string> Events = new();

    public void Add(MarsFrame f)
    {
        Frames++;
        if (!double.IsNaN(_lastDeviceMs))
        {
            double step = 1000.0 / f.Fps;
            int missing = (int)Math.Round((f.DeviceTimeMs - _lastDeviceMs) / step) - 1;
            if (missing > 0) { MissingFrames += missing; Log(f, $"{missing} Bild(er) fehlen"); }
        }
        _lastDeviceMs = f.DeviceTimeMs;

        foreach (var d in f.Devices)
        {
            if (!_first.ContainsKey(d.Serial)) _first[d.Serial] = d;
            if (_last.TryGetValue(d.Serial, out var prev))
            {
                if (d.IsBaseStation)
                {
                    double shift = Dist(d, _first[d.Serial]);
                    if (shift > BaseShiftLimitM && Dist(prev, _first[d.Serial]) <= BaseShiftLimitM)
                    {
                        BaseShifts++;
                        Log(f, $"Basisstation {d.Serial} verschoben um {shift * 1000:F1} mm — Kartensprung");
                    }
                }
                else if (Dist(d, prev) > JumpLimitM)
                {
                    Jumps++;
                    Log(f, $"Sprung {d.Serial}: {Dist(d, prev) * 100:F1} cm in einem Bild");
                }
            }
            _last[d.Serial] = d;
        }
    }

    public double BaseStationDriftMm(string serial) =>
        _first.TryGetValue(serial, out var a) && _last.TryGetValue(serial, out var b) ? Dist(a, b) * 1000 : 0;

    void Log(MarsFrame f, string text) { lock (Events) Events.Add($"[{f.Tc}] {text}"); }

    static double Dist(in MarsDevice a, in MarsDevice b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
