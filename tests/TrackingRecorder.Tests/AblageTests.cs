using TrackingRecorder.Core;

namespace TrackingRecorder.Tests;

public class AblageTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "tr-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact]
    public void StammNachSystemkarte() =>
        Assert.Equal("STUDIO-06_T03_01M5ADCN", Ablage.Stamm("STUDIO-06", 3, "01M5ADCN"));

    [Fact]
    public void StammOhneSonderzeichen() =>
        Assert.Equal("Sz_12_a_T01_X", Ablage.Stamm("Sz 12/a", 1, "X"));

    [Fact]
    public void ZielNimmtVorhandenenTagesordner()
    {
        var ablage = Tmp();
        Directory.CreateDirectory(Path.Combine(ablage, "VKH", "2026-11-10_STUDIO"));
        Directory.CreateDirectory(Path.Combine(ablage, "VKH", "2026-11-09_STUDIO"));
        var ziel = Ablage.Ziel(ablage, "VKH", new DateTime(2026, 11, 10));
        Assert.Equal(Path.Combine(ablage, "VKH", "2026-11-10_STUDIO", "08_TRACKING"), ziel);
    }

    [Fact]
    public void ZielOhneOrdnerBautStudioname()
    {
        var ablage = Tmp();
        Assert.Equal(Path.Combine(ablage, "VKH", "2026-11-10_STUDIO", "08_TRACKING"), Ablage.Ziel(ablage, "VKH", new DateTime(2026, 11, 10)));
    }

    [Fact]
    public void KopierenUeberschreibtNie()
    {
        var quelle = Tmp(); var ziel = Tmp();
        var csv = Path.Combine(quelle, "a.csv"); File.WriteAllText(csv, "1");
        var raw = Path.Combine(quelle, "a.raw.txt"); File.WriteAllText(raw, "r");
        Assert.Equal("T", Ablage.Kopieren(new[] { csv, raw }, ziel, "T"));
        File.WriteAllText(csv, "2");
        Assert.Equal("T_2", Ablage.Kopieren(new[] { csv, raw }, ziel, "T"));
        Assert.Equal("1", File.ReadAllText(Path.Combine(ziel, "T.csv")));
        Assert.Equal("2", File.ReadAllText(Path.Combine(ziel, "T_2.csv")));
        Assert.True(File.Exists(Path.Combine(ziel, "T_2.raw.txt")));
        Assert.Empty(Directory.GetFiles(ziel, "*.teil"));
    }
}
