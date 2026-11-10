using System.Collections.Concurrent;
using System.Globalization;
using TrackingRecorder.Core;

// Erste Testversion: FreeD empfangen, live anzeigen, optional als CSV aufnehmen.
//   trackingrecorder [--port 6001] [--fps 25] [--out aufnahme.csv]
//   trackingrecorder mitschnitt [--relay 10.10.10.70:7001] [--out datei.txt]   Rohmitschnitt FreeD + Live Link
if (args.Length > 0 && args[0] == "mitschnitt") return TrackingRecorder.Cli.Mitschnitt.Run(args);

int port = 6001;
double fps = 25;
string? outPath = null;
for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--port": port = int.Parse(args[++i]); break;
        case "--fps": fps = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--out": outPath = args[++i]; break;
    }
}

var tc = new SystemClockTimecode(fps);
using var recorder = outPath is null ? null : new CsvRecorder(outPath, tc.Source);
using var rx = new FreeDReceiver(port, tc);

var latest = new ConcurrentDictionary<byte, Sample>();
var counts = new ConcurrentDictionary<byte, long>();
rx.SampleReceived += s =>
{
    recorder?.Write(s);
    latest[s.Pose.CameraId] = s;
    counts.AddOrUpdate(s.Pose.CameraId, 1, (_, n) => n + 1);
};

var stop = new ManualResetEventSlim();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
Console.WriteLine($"Lausche auf UDP {port} · {fps} fps · Timecode-Quelle: {tc.Source}" +
                  (outPath is null ? "" : $" · Aufnahme: {outPath}") + " · Ctrl+C beendet");

var prev = new Dictionary<byte, long>();
while (!stop.Wait(1000))
{
    foreach (var (id, s) in latest.OrderBy(k => k.Key))
    {
        long n = counts[id];
        long rate = n - prev.GetValueOrDefault(id);
        prev[id] = n;
        var p = s.Pose;
        Console.WriteLine(FormattableString.Invariant(
            $"[{s.Tc}] Rover {id}: {rate,3} Hz  X {p.XMm,9:F1} Y {p.YMm,9:F1} Z {p.ZMm,9:F1} mm  Pan {p.PanDeg,8:F2} Tilt {p.TiltDeg,7:F2} Roll {p.RollDeg,7:F2}°  Zoom {p.Zoom} Fokus {p.Focus} Spare {p.Spare:X4}"));
    }
    if (rx.Invalid > 0) Console.WriteLine($"  ungültige Pakete bisher: {rx.Invalid}");
}
Console.WriteLine($"Ende. {rx.Received} Pakete empfangen.");
return 0;
