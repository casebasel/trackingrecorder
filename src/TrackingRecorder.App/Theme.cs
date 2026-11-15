using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace TrackingRecorder.App;

/// <summary>
/// Farben nach Design System „Filmstudio Stage", Familie Set-Apps (wie Stage Ingest; Marlon 15.11.2026):
/// Graphit-Flächen, Akzent nachts Bernstein und tags Blau, Grün für bereit/verbunden/bewiesen,
/// Gold nur für Störung, Rot nur für laufende Aufnahme. Werte = tokens.json (OKLCH) in sRGB.
/// Nacht = Windows dunkel, Tag = Windows hell.
/// </summary>
public static class Theme
{
    static readonly (string Key, string Tag, string Nacht)[] Werte =
    {
        ("Fenster",      "#D6D8DA", "#0A0B0D"),
        ("Panel",        "#F6F7F8", "#131416"),
        ("Panel2",       "#E8EAEC", "#181A1C"),
        ("Feld",         "#FFFFFF", "#08090B"),
        ("Hover",        "#E0E3E7", "#1D1F22"),
        ("Linie",        "#C1C4C8", "#292B2E"),
        ("LinieStark",   "#73787D", "#63666B"),
        ("Text",         "#111418", "#EAEBED"),
        ("Text2",        "#2F3338", "#C1C4C8"),
        ("Text3",        "#494D53", "#A9ADB1"),
        ("TextAus",      "#717579", "#717579"),
        ("Akzent",       "#0961BB", "#E09036"),
        ("AkzentHover",  "#0052AB", "#F1A048"),
        ("AkzentText",   "#0052AB", "#F3AE58"),
        ("AufAkzent",    "#FCFCFC", "#0E0A07"),
        ("Auswahl",      "#BBD7F8", "#4A2E10"),
        ("Ok",           "#006E30", "#61CB7C"),
        ("Warn",         "#895200", "#F3B94C"),
        ("Rot",          "#AC1922", "#FA706A"),
        ("RotFlaeche",   "#B71824", "#A90017"),
        ("RotFlaecheGedrueckt", "#A50013", "#970003"),
        ("AufRot",       "#FCFCFC", "#FCFCFC"),
    };

    public static bool Dunkel { get; private set; }

    /// <summary>Nach jedem Wechsel (Zeichen zeichnen sich neu).</summary>
    public static event Action? Geaendert;

    public static void Apply() => Apply(null);

    /// <summary>null = nach Windows, sonst erzwungen (für Prüfbilder).</summary>
    public static void Apply(bool? erzwingen)
    {
        Dunkel = erzwingen ?? SystemIsDark();
        var res = Application.Current.Resources;
        foreach (var (key, tag, nacht) in Werte)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Dunkel ? nacht : tag));
            brush.Freeze();
            res[key] = brush;
        }
        // schatten-taste: einziger Schatten, 1-px-Kontaktschatten normaler Tasten
        var schatten = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 2, ShadowDepth = 1, Direction = 270, Color = Colors.Black, Opacity = Dunkel ? 0.35 : 0.12 };
        schatten.Freeze();
        res["SchattenTaste"] = schatten;
        Geaendert?.Invoke();
    }

    public static void Follow()
    {
        Apply();
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General)
                Application.Current.Dispatcher.Invoke(Apply);
        };
    }

    public static Brush Get(string key) => (Brush)Application.Current.Resources[key];

    static bool SystemIsDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }
}
