using Velopack;

namespace TrackingRecorder.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Muss als Erstes laufen: Velopack erledigt hier Installation, Update-Schritte und Deinstallation.
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
