using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TrackingRecorder.App;

public partial class App : Application
{
    Mutex? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = e.Args;
        int i = Array.IndexOf(args, "--bild");
        if (i >= 0 && i + 1 < args.Length)
        {
            // Prüfbild ohne Anzeige (auch per SSH): echte Daten abwarten, Fenster rendern, beenden.
            //   TrackingRecorderApp.exe --bild C:\claude\tmp\ui.png [--dunkel|--hell] [--rec]
            Theme.Apply(args.Contains("--dunkel") ? true : args.Contains("--hell") ? false : null);
            RenderBild(args[i + 1], args.Contains("--rec"));
            return;
        }
        // Nur eine Instanz: zwei Fenster würden sich bei Mars denselben Platz (Port) teilen.
        _single = new Mutex(true, @"Local\TrackingRecorderApp", out bool first);
        if (!first)
        {
            MessageBox.Show("TrackingRecorder läuft schon. Das offene Fenster benutzen.", "TrackingRecorder");
            Shutdown();
            return;
        }
        Theme.Follow();
        new MainWindow().Show();
    }

    void RenderBild(string path, bool rec)
    {
        var w = new MainWindow(localPort: 55501);   // eigener Port, damit ein offenes Fenster nicht gestört wird
        var root = (FrameworkElement)w.Content;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool started = false;
        double readyAt = -1;
        timer.Tick += (_, _) =>
        {
            // warten, bis Datenstrom und Web-Zustand da sind (höchstens 10 s)
            if (readyAt < 0 && w.HasMarsStatus) readyAt = sw.Elapsed.TotalSeconds + 0.3;
            bool ready = readyAt >= 0 || sw.Elapsed.TotalSeconds > 10;
            if (!ready || sw.Elapsed.TotalSeconds < 2) return;
            if (rec && !started) { w.StartForPreview(); started = true; readyAt = sw.Elapsed.TotalSeconds + 1000; return; }
            if (rec && sw.Elapsed.TotalSeconds < readyAt - 1000 + 3) return;
            timer.Stop();
            File.WriteAllText(Path.ChangeExtension(path, ".txt"), $"Web-Zustand nach {(w.HasMarsStatus ? (readyAt >= 1000 ? readyAt - 1000 : readyAt).ToString("F1") : "—")} s");
            var size = new Size(w.Width, w.Height);
            var host = new System.Windows.Controls.Border { Background = Theme.Get("Grund"), Width = size.Width, Height = size.Height };
            w.Content = null;
            host.Child = root;
            host.Measure(size); host.Arrange(new Rect(size)); host.UpdateLayout();
            const double scale = 2;
            var bmp = new RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bmp.Render(host);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = File.Create(path)) enc.Save(fs);
            if (rec) w.StopForPreview();
            w.Shutdown();
            Shutdown();
        };
        timer.Start();
    }
}
