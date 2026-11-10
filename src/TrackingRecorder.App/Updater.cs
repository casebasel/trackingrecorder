using Velopack;
using Velopack.Sources;

namespace TrackingRecorder.App;

/// <summary>
/// Updates über Velopack aus den GitHub-Releases (öffentliches Repo casebasel/trackingrecorder).
/// Ablauf: suchen → im Hintergrund laden → beim Schliessen der App einspielen (nie während einer Aufnahme).
/// „Neu starten" spielt sofort ein. Nicht installierte Kopien (z. B. die exe in C:\claude\…) melden das nur.
/// </summary>
public sealed class Updater
{
    const string Repo = "https://github.com/casebasel/trackingrecorder";
    readonly UpdateManager _mgr = new(new GithubSource(Repo, null, false));
    UpdateInfo? _bereit;

    public bool Installiert => _mgr.IsInstalled;
    public string Version => _mgr.CurrentVersion?.ToString()
                             ?? typeof(Updater).Assembly.GetName().Version?.ToString(3) ?? "?";
    public string? BereitVersion => _bereit?.TargetFullRelease.Version.ToString();
    public string Zustand { get; private set; } = "";

    public async Task PruefenAsync()
    {
        if (!_mgr.IsInstalled) { Zustand = "nicht installiert – keine Updates"; return; }
        if (_bereit is not null) return;
        try
        {
            Zustand = "suche Update …";
            var info = await _mgr.CheckForUpdatesAsync();
            if (info is null) { Zustand = "aktuell"; return; }
            Zustand = $"lade {info.TargetFullRelease.Version} …";
            await _mgr.DownloadUpdatesAsync(info);
            _bereit = info;
            Zustand = $"Update {info.TargetFullRelease.Version} bereit";
        }
        catch (Exception ex)
        {
            Zustand = "Update-Suche fehlgeschlagen";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    /// <summary>Beim Beenden aufrufen: spielt ein geladenes Update ein, sobald die App zu ist (ohne Neustart).</summary>
    public void BeimBeendenEinspielen()
    {
        if (_bereit is not null) _mgr.WaitExitThenApplyUpdates(_bereit, silent: true, restart: false);
    }

    public void JetztNeuStarten()
    {
        if (_bereit is not null) _mgr.ApplyUpdatesAndRestart(_bereit);
    }
}
