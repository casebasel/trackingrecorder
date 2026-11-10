namespace TrackingRecorder.Core;

/// <summary>SMPTE-Timecode ohne Drop-Frame (25/50/24/30 fps). Drop-Frame folgt, falls nötig.</summary>
public readonly record struct Timecode(int Hours, int Minutes, int Seconds, int Frames, double Fps)
{
    public static Timecode FromTimeOfDay(TimeSpan t, double fps)
    {
        int nominal = (int)Math.Round(fps);
        long totalFrames = (long)Math.Floor(t.TotalSeconds * nominal);
        int frames = (int)(totalFrames % nominal);
        long secs = totalFrames / nominal;
        return new Timecode((int)(secs / 3600 % 24), (int)(secs / 60 % 60), (int)(secs % 60), frames, fps);
    }

    public override string ToString() => $"{Hours:00}:{Minutes:00}:{Seconds:00}:{Frames:00}";
}

/// <summary>Woher der Timecode einer Probe stammt — wird in jede Aufnahme geschrieben.</summary>
public enum TimecodeSource
{
    /// <summary>Uhr des Aufnahme-PCs. Nur Notlösung, nicht framegenau zur Kamera.</summary>
    SystemClock,
    /// <summary>LTC-Audiosignal am Aufnahme-PC (noch nicht umgesetzt).</summary>
    Ltc,
}

public interface ITimecodeProvider
{
    TimecodeSource Source { get; }
    Timecode Now();
}

public sealed class SystemClockTimecode(double fps) : ITimecodeProvider
{
    public TimecodeSource Source => TimecodeSource.SystemClock;
    public Timecode Now() => Timecode.FromTimeOfDay(DateTime.Now.TimeOfDay, fps);
}
