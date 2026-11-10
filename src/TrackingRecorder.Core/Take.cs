using System.Text.Json;

namespace TrackingRecorder.Core;

/// <summary>Zusammenfassung eines Takes, liegt als &lt;take&gt;.json neben der CSV. Grundlage für Export und Prüfung.</summary>
public sealed record TakeInfo(
    string Name,
    string CsvFile,
    DateTime StartUtc,
    DateTime EndUtc,
    string StartTimecode,
    string EndTimecode,
    double Fps,
    long Frames,
    long MissingFrames,
    long Jumps,
    long BaseShifts,
    IReadOnlyList<string> Devices,
    IReadOnlyList<string> Events,
    string App = "TrackingRecorder",
    string? StageTakeId = null,
    string? StageStartTc = null,
    int VorlaufBilder = 0,
    string Ausloeser = "Hand")
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
}

public static class TakeNames
{
    /// <summary>„Take_001" → „Take_002"; ohne Zahl am Ende wird „_002" angehängt.</summary>
    public static string Next(string name)
    {
        int i = name.Length;
        while (i > 0 && char.IsDigit(name[i - 1])) i--;
        if (i == name.Length) return name + "_002";
        string digits = name[i..];
        return name[..i] + (long.Parse(digits) + 1).ToString().PadLeft(digits.Length, '0');
    }

    /// <summary>Entfernt Zeichen, die Windows in Dateinamen nicht erlaubt.</summary>
    public static string Safe(string name)
    {
        var bad = Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }).ToHashSet();
        var s = new string(name.Trim().Select(c => bad.Contains(c) ? '_' : c).ToArray());
        return s.Length == 0 ? "Take" : s;
    }
}
