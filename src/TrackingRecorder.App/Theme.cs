using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace TrackingRecorder.App;

/// <summary>
/// Farben hell/dunkel nach der Windows-Einstellung „App-Modus". Werte: syncomat src/App.css v0.9.39 (sRGB),
/// Signalregeln aus stage-system DESIGN-GEMEINSAM.md: Rot nur für laufende Aufnahme, Gold für Störung, Grün für ok.
/// </summary>
public static class Theme
{
    static readonly (string Key, string Hell, string Dunkel)[] Werte =
    {
        ("Grund",      "#FFFFFF", "#0E0F12"),
        ("Flaeche",    "#F4F5F7", "#17191D"),
        ("Linie",      "#D9DCE1", "#2A2D33"),
        ("LinieFein",  "#ECEEF1", "#1E2126"),
        ("Text",       "#111317", "#ECEEF1"),
        ("Text2",      "#5B616B", "#9AA1AC"),
        ("Kopf",       "#111317", "#000000"),
        ("KopfText",   "#FFFFFF", "#FFFFFF"),
        ("KopfText2",  "#B8BEC8", "#9AA1AC"),
        ("Blau",       "#2867E4", "#568EF9"),
        ("BlauFlaeche","#E4EFFF", "#162644"),
        ("Warn",       "#895200", "#F3B94C"),
        ("WarnAufKopf","#F3B94C", "#F3B94C"),
        ("Rot",        "#AC1922", "#FA706A"),
        ("RotFlaeche", "#B3232B", "#A8282A"),   // DESIGN-GEMEINSAM rot-flaeche (Tag/Nacht), Weiss darauf ≥ 6:1
        ("Ok",         "#006E30", "#61CB7C"),
    };

    public static bool Dunkel { get; private set; }

    public static void Apply() => Apply(null);

    /// <summary>null = nach Windows, sonst erzwungen (für Prüfbilder).</summary>
    public static void Apply(bool? erzwingen)
    {
        Dunkel = erzwingen ?? SystemIsDark();
        var res = Application.Current.Resources;
        foreach (var (key, hell, dunkel) in Werte)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Dunkel ? dunkel : hell));
            brush.Freeze();
            res[key] = brush;
        }
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
