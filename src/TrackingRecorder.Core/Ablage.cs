using System.Text.Json;

namespace TrackingRecorder.Core;

/// <summary>
/// Ablage der Studio-Takes im Footage-Bereich (Systemkarte stage-system, Marlon 10.11.2026, Commit 4223b1b):
/// &lt;Ablage&gt;/&lt;PROJEKT&gt;/&lt;Drehordner des Aufnahmetags&gt;/08_TRACKING/. Ablage = Ordner mit der Merkdatei
/// <c>.stage-ingest-ablage.json</c> des Ingest. Drehordner wie bei den Renders der Stage: vorhandener Ordner des
/// Aufnahmetags, sonst &lt;JJJJ-MM-TT&gt;_&lt;Drehort&gt;; anlegen, wenn er fehlt. Erst lokal aufnehmen, dann kopieren,
/// nie überschreiben (_2, _3 …). Nur 08_TRACKING wird beschrieben.
/// </summary>
public static class Ablage
{
    public const string Merkdatei = ".stage-ingest-ablage.json";
    public const string Unterordner = "08_TRACKING";
    public const string Studio = "STUDIO";

    /// <summary>Weitere Orte, die ohne verbundenes Laufwerk geprüft werden.</summary>
    public static readonly string[] Unc = { @"\\TRUENAS\Footage", @"\\TRUENAS\Footage\ingest" };

    /// <summary>Sucht die Ablage des Ingest: Laufwerke A–Z bis zwei Ebenen tief (wie das Unreal-Plugin der Stage), dann UNC.</summary>
    public static string? Finden()
    {
        static bool Hat(string p) { try { return File.Exists(Path.Combine(p, Merkdatei)); } catch { return false; } }
        static IEnumerable<string> Kinder(string p)
        {
            try
            {
                return Directory.GetDirectories(p).Where(d =>
                {
                    var n = Path.GetFileName(d);
                    return !n.StartsWith('$') && !n.StartsWith('.');
                });
            }
            catch { return Array.Empty<string>(); }
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                string root;
                try { if (!drive.IsReady) continue; root = drive.RootDirectory.FullName; } catch { continue; }
                if (Hat(root)) return root;
                foreach (var e1 in Kinder(root))
                {
                    if (Hat(e1)) return e1;
                    foreach (var e2 in Kinder(e1)) if (Hat(e2)) return e2;
                }
            }
        }
        return Unc.FirstOrDefault(Hat);
    }

    /// <summary>Zielordner 08_TRACKING für Projekt und Aufnahmetag; legt nichts an.</summary>
    public static string Ziel(string ablage, string projektKurz, DateTime tag, string drehort = Studio)
    {
        string projekt = Path.Combine(ablage, Ordnername(projektKurz));
        string prefix = tag.ToString("yyyy-MM-dd") + "_";
        string? vorhanden = null;
        try
        {
            var tagesordner = Directory.Exists(projekt)
                ? Directory.GetDirectories(projekt).Select(Path.GetFileName).Where(n => n!.StartsWith(prefix)).ToList()
                : new List<string?>();
            vorhanden = tagesordner.FirstOrDefault(n => n!.Equals(prefix + drehort, StringComparison.OrdinalIgnoreCase))
                        ?? (tagesordner.Count == 1 ? tagesordner[0] : null);
        }
        catch { }
        return Path.Combine(projekt, vorhanden ?? prefix + Ordnername(drehort), Unterordner);
    }

    /// <summary>
    /// Kopiert die Dateien eines Takes (gleicher Stamm, verschiedene Endungen) unter neuem Stamm nach <paramref name="ziel"/>.
    /// Nie überschreiben: belegt ein Name schon eine Datei, bekommt der ganze Take _2, _3 … Gibt den benutzten Stamm zurück.
    /// </summary>
    public static string Kopieren(IReadOnlyList<string> dateien, string ziel, string stamm)
    {
        Directory.CreateDirectory(ziel);
        string Name(int n) => n == 1 ? stamm : $"{stamm}_{n}";
        int nr = 1;
        while (dateien.Any(d => File.Exists(Path.Combine(ziel, Name(nr) + Endung(d))))) nr++;
        foreach (var d in dateien)
        {
            string dest = Path.Combine(ziel, Name(nr) + Endung(d));
            string tmp = dest + ".teil";
            File.Copy(d, tmp, overwrite: true);   // erst vollständig kopieren, dann umbenennen
            File.Move(tmp, dest, overwrite: false);
        }
        return Name(nr);
    }

    /// <summary>„.raw.txt" bleibt eine Endung.</summary>
    static string Endung(string pfad)
    {
        string n = Path.GetFileName(pfad);
        return n.EndsWith(".raw.txt", StringComparison.OrdinalIgnoreCase) ? ".raw.txt" : Path.GetExtension(n);
    }

    /// <summary>Ordnername wie der Ingest: verbotene Zeichen → „_", keine Punkte/Leerzeichen am Ende.</summary>
    public static string Ordnername(string s)
    {
        var bad = "<>:\"/\\|?*".ToHashSet();
        var t = new string(s.Select(c => bad.Contains(c) || c < 32 ? '_' : c).ToArray());
        return System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").TrimEnd('.', ' ').Trim();
    }

    /// <summary>Dateistamm nach Systemkarte: ohne Clipname &lt;Slate&gt;_T&lt;NN&gt;_&lt;studio_take.id&gt;, ASCII, keine Leerzeichen.</summary>
    public static string Stamm(string? slate, int nummer, string takeId)
    {
        string s = string.IsNullOrWhiteSpace(slate) ? "Take" : slate;
        s = new string(s.Select(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' ? c : '_').ToArray());
        return $"{s}_T{nummer:00}_{takeId}";
    }
}

/// <summary>Takes, die noch aufs NAS müssen (z. B. weil es nicht erreichbar war). Liegt als JSON neben den Einstellungen.</summary>
public sealed class AblageAuftraege
{
    public sealed record Auftrag(string Csv, string Projekt, DateTime Tag, string Stamm, string? AbgelegtIn = null, string? Fehler = null);

    readonly string _pfad;
    public List<Auftrag> Offen { get; private set; } = new();

    public AblageAuftraege(string pfad)
    {
        _pfad = pfad;
        try { if (File.Exists(pfad)) Offen = JsonSerializer.Deserialize<List<Auftrag>>(File.ReadAllText(pfad)) ?? new(); }
        catch { Offen = new(); }
    }

    readonly object _lock = new();

    public void Add(Auftrag a) { lock (_lock) { Offen.Add(a); Save(); } }

    /// <summary>Versucht alle offenen Aufträge; gibt die erledigten zurück.</summary>
    public List<Auftrag> Abarbeiten()
    {
        var fertig = new List<Auftrag>();
        lock (_lock) { if (Offen.Count == 0) return fertig; }
        string? ablage = Ablage.Finden();
        lock (_lock) return AbarbeitenIn(ablage, fertig);
    }

    List<Auftrag> AbarbeitenIn(string? ablage, List<Auftrag> fertig)
    {
        foreach (var a in Offen.ToList())
        {
            if (ablage is null) { Ersetzen(a, a with { Fehler = "Ablage des Ingest nicht gefunden (NAS verbunden?)" }); continue; }
            try
            {
                var dateien = new[] { a.Csv, Path.ChangeExtension(a.Csv, ".raw.txt"), Path.ChangeExtension(a.Csv, ".json") }
                    .Where(File.Exists).ToList();
                if (dateien.Count == 0) { Offen.Remove(a); continue; }
                string ziel = Ablage.Ziel(ablage, a.Projekt, a.Tag);
                string stamm = Ablage.Kopieren(dateien, ziel, a.Stamm);
                Offen.Remove(a);
                fertig.Add(a with { AbgelegtIn = Path.Combine(ziel, stamm), Fehler = null });
            }
            catch (Exception ex) { Ersetzen(a, a with { Fehler = ex.Message }); }
        }
        Save();
        return fertig;
    }

    void Ersetzen(Auftrag alt, Auftrag neu) { int i = Offen.IndexOf(alt); if (i >= 0) Offen[i] = neu; }

    void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_pfad)!);
            File.WriteAllText(_pfad, JsonSerializer.Serialize(Offen, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
