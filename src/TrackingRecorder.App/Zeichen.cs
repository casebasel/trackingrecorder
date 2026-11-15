using System.Windows;
using System.Windows.Media;

namespace TrackingRecorder.App;

/// <summary>
/// Zustandszeichen nach „Filmstudio Stage" (Lampe), 14 px, immer mit Wort daneben:
/// ok = Kreis-Haken (Grün), warn = Dreieck (Gold), unbekannt = gestrichelter Ring (linie-stark, ohne Gold),
/// rec = gefüllter Punkt (Rot), aus = gefüllter Punkt (text-3, letzter Stand ohne Bewertung).
/// </summary>
public sealed class Zeichen : FrameworkElement
{
    public static readonly DependencyProperty ArtProperty = DependencyProperty.Register(
        nameof(Art), typeof(string), typeof(Zeichen), new FrameworkPropertyMetadata("unbekannt", FrameworkPropertyMetadataOptions.AffectsRender));

    public string Art { get => (string)GetValue(ArtProperty); set => SetValue(ArtProperty, value); }

    public Zeichen()
    {
        Width = Height = 14;
        // Farben folgen dem Theme: bei jedem Wechsel neu zeichnen
        Loaded += (_, _) => Theme.Geaendert += InvalidateVisual;
        Unloaded += (_, _) => Theme.Geaendert -= InvalidateVisual;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double s = Math.Min(ActualWidth, ActualHeight), c = s / 2;
        Brush B(string k) => (Brush)(TryFindResource(k) ?? Brushes.Gray);
        switch (Art)
        {
            case "ok":
            {
                var pen = new Pen(B("Ok"), 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                dc.DrawEllipse(null, pen, new Point(c, c), c - 1, c - 1);
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(s * 0.30, s * 0.52), false, false);
                    ctx.LineTo(new Point(s * 0.45, s * 0.66), true, false);
                    ctx.LineTo(new Point(s * 0.71, s * 0.37), true, false);
                }
                dc.DrawGeometry(null, pen, g);
                break;
            }
            case "warn":
            {
                var pen = new Pen(B("Warn"), 1.6) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(c, 1.2), false, true);
                    ctx.LineTo(new Point(s - 0.8, s - 1.4), true, true);
                    ctx.LineTo(new Point(0.8, s - 1.4), true, true);
                }
                dc.DrawGeometry(null, pen, g);
                dc.DrawLine(pen, new Point(c, s * 0.40), new Point(c, s * 0.62));
                dc.DrawEllipse(B("Warn"), null, new Point(c, s * 0.78), 0.9, 0.9);
                break;
            }
            case "rec":
                dc.DrawEllipse(B("Rot"), null, new Point(c, c), c * 0.72, c * 0.72);
                break;
            case "aus":
                dc.DrawEllipse(B("Text3"), null, new Point(c, c), c * 0.55, c * 0.55);
                break;
            default: // unbekannt: gestrichelter Ring, ohne warn
            {
                var pen = new Pen(B("LinieStark"), 1.4) { DashStyle = new DashStyle(new double[] { 1.6, 1.6 }, 0) };
                dc.DrawEllipse(null, pen, new Point(c, c), c - 1.5, c - 1.5);
                break;
            }
        }
    }
}
