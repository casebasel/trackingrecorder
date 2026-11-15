using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TrackingRecorder.Core;

namespace TrackingRecorder.App;

public partial class MainWindow : Window
{
    // Bekannte Geräte im Studio (Befund 10.11.2026). Unbekannte erscheinen mit Seriennummer.
    static readonly Dictionary<string, string> KnownRoles = new()
    {
        ["LHR-0FFCBEE6"] = "Kamera",
        ["LHR-16D49F61"] = "Kalibrier-Tracker",
    };

    static Brush Warn => Theme.Get("Warn");
    static Brush Leise => Theme.Get("Text3");

    // Halten zum Stoppen (Set-Apps): 0,6 s, Füllung von links in rot-flaeche-gedrueckt; kurzes Tippen stoppt nicht.
    const double HaltenSek = 0.6;
    readonly DispatcherTimer _halten = new() { Interval = TimeSpan.FromMilliseconds(15) };
    readonly System.Diagnostics.Stopwatch _haltenUhr = new();
    bool _haltenGestoppt;

    readonly ObservableCollection<DeviceRow> _devices = new();
    readonly ObservableCollection<EventRow> _events = new();
    readonly DispatcherTimer _ui = new() { Interval = TimeSpan.FromMilliseconds(100) };
    readonly object _lock = new();
    readonly Settings _settings = Settings.Load();

    MarsClient? _client;
    MarsRemote? _remote;
    readonly Updater _updater = new();
    readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromMinutes(30) };
    StageListener? _stage;
    readonly AblageAuftraege _ablage = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrackingRecorder", "ablage-offen.json"));
    readonly DispatcherTimer _ablageTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    string? _ablageOrt;
    bool _ablageLaeuft;
    DateTime _takeStartLokal;
    DateTime? _letzterTakeLokal;   // Ende des letzten Takes, für den nächtlichen Update-Neustart
    readonly DispatcherTimer _nachtTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    readonly Queue<MarsSample> _vorlauf = new();   // letzte 2 s, damit ein spätes REC nichts abschneidet
    const int VorlaufMax = 50;
    StageTake? _stageTake;
    string _ausloeser = "Hand";
    int _vorlaufBilder;
    MarsRemote.Status? _status;
    string? _remoteError;
    bool _polling;
    readonly ObservableCollection<StatusRow> _mars = new();
    readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly Queue<double> _frameTimes = new();
    string? _connectError;
    MarsFrame? _latest;
    double _latestMono;
    readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    MarsMonitor _live = new();
    long _rate;

    // laufende Aufnahme
    MarsRecorder? _recorder;
    MarsMonitor? _takeMonitor;
    string? _takeName, _takeCsv, _startTc;
    DateTime _takeStartUtc;
    int _shownEvents;

    readonly int _localPort;

    public MainWindow(int localPort = MarsClient.DefaultLocalPort)
    {
        _localPort = localPort;
        InitializeComponent();
        DeviceList.ItemsSource = _devices;
        EventList.ItemsSource = _events;
        IpBox.Text = _settings.MarsIp;
        FolderBox.Text = _settings.Folder;
        TakeBox.Text = _settings.NextTake;
        MarsList.ItemsSource = _mars;
        AchsenKopf.Text = string.Format(CultureInfo.InvariantCulture, "{0,7}  {1,7}  {2,7} m", "X", "Y", "Z");
        _halten.Tick += (_, _) => HaltenTick();
        RecButton.Tag = 0.0;
        _poll.Tick += async (_, _) => await PollRemote();
        _poll.Start();
        Version.Text = "v" + _updater.Version;
        KopfVersion.Text = "v" + _updater.Version;
        Title = $"TrackingRecorder {_updater.Version}";
        _updateTimer.Tick += async (_, _) => await UpdatePruefen();
        _updateTimer.Start();
        _nachtTimer.Tick += (_, _) => NachtsEinspielen();
        _nachtTimer.Start();
        Loaded += async (_, _) => await UpdatePruefen();
        _ui.Tick += (_, _) => Refresh();
        _ui.Start();
        Connect();
        KlappeBox.IsChecked = _settings.MitKlappe;
        ConnectStage();
        _ablageTimer.Tick += async (_, _) => await AblageAbarbeiten();
        _ablageTimer.Start();
        Loaded += async (_, _) => await AblageAbarbeiten();
    }

    // ---------- Verbindung ----------

    void Connect()
    {
        _client?.Dispose();
        _latest = null;
        _live = new MarsMonitor();
        _connectError = null;
        if (!IPAddress.TryParse(IpBox.Text.Trim(), out var ip))
        {
            _connectError = "Mars-IP ist keine gültige Adresse.";
            return;
        }
        try
        {
            _remote?.Dispose();
            _remote = new MarsRemote(ip.ToString());
            _status = null;
            _ = PollRemote();
            _client = new MarsClient(ip, _localPort);
            _client.FrameReceived += OnFrame;
            PortText.Text = $"{ip}:{MarsProtocol.Port} · eigener Port {_client.LocalPort}";
        }
        catch (Exception ex)
        {
            _client = null;
            _connectError = ex is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.AddressAlreadyInUse }
                ? $"Port {_localPort} ist belegt — läuft der TrackingRecorder schon?"
                : "Verbindung nicht möglich: " + ex.Message;
        }
    }

    void OnReconnect(object sender, RoutedEventArgs e)
    {
        if (_recorder is not null) { MessageBox.Show(this, "Zuerst die Aufnahme stoppen."); return; }
        _settings.MarsIp = IpBox.Text.Trim();
        _settings.Save();
        Connect();
    }

    // Empfangs-Thread: nur speichern und zählen, keine UI.
    void OnFrame(MarsSample s)
    {
        lock (_lock)
        {
            if (_recorder is not null) _recorder.Write(s);
            else { _vorlauf.Enqueue(s); while (_vorlauf.Count > VorlaufMax) _vorlauf.Dequeue(); }
            _takeMonitor?.Add(s.Frame);
            _live.Add(s.Frame);
            _latest = s.Frame;
            // eigene Uhr des Fensters, nicht die des Empfängers — sonst passen Alter und Rate nicht
            double now = _clock.Elapsed.TotalMilliseconds;
            _latestMono = now;
            _frameTimes.Enqueue(now);
            while (_frameTimes.Count > 0 && now - _frameTimes.Peek() > 1000) _frameTimes.Dequeue();
        }
    }

    // ---------- Anzeige (10× pro Sekunde) ----------

    void Refresh()
    {
        MarsFrame? f;
        double age;
        lock (_lock)
        {
            f = _latest;
            age = _clock.Elapsed.TotalMilliseconds - _latestMono;
            double now = _clock.Elapsed.TotalMilliseconds;
            while (_frameTimes.Count > 0 && now - _frameTimes.Peek() > 1000) _frameTimes.Dequeue();
            _rate = _frameTimes.Count;
        }
        bool connected = f is not null && age < 500;
        bool recording = _recorder is not null && _takeMonitor is not null;
        var monitor = _takeMonitor ?? _live;
        long stoerungen = monitor.MissingFrames + monitor.Jumps + monitor.BaseShifts;

        // Kopfzeile: zwei Zahlen, Störung als Gold-Zusatz
        KopfRate.Text = connected ? _rate.ToString(CultureInfo.InvariantCulture) : "0";
        KopfGeraete.Text = f?.Devices.Count.ToString(CultureInfo.InvariantCulture) ?? "0";
        KopfStoerung.Visibility = stoerungen > 0 ? Visibility.Visible : Visibility.Collapsed;
        KopfStoerungText.Text = stoerungen == 1 ? "1 Störung" : $"{stoerungen} Störungen";

        // Lage in einem Satz
        if (recording)
        {
            var dauer = DateTime.UtcNow - _takeStartUtc;
            LageZeichen.Art = "rec";
            LageText.Text = $"Aufnahme läuft · {_takeName} · {dauer:mm\\:ss}";
            LageMeta.Text = $"seit {_startTc} · {_takeMonitor!.Frames} Bilder · fehlend {_takeMonitor.MissingFrames} · Sprünge {_takeMonitor.Jumps} · Kartensprünge {_takeMonitor.BaseShifts}";
            RecDot.Opacity = DateTime.UtcNow.Millisecond < 600 ? 1 : 0.4;
        }
        else if (_connectError is not null)
        {
            LageZeichen.Art = "warn";
            LageText.Text = _connectError;
            LageMeta.Text = "IP unten in der Statusleiste prüfen, dann „Neu verbinden“.";
        }
        else if (connected)
        {
            LageZeichen.Art = "ok";
            LageText.Text = "Mars verbunden. Bereit zur Aufnahme.";
            LageMeta.Text = $"Mars sendet {f!.Fps:0.##} Bilder/s · Takes werden in {FolderBox.Text} gespeichert";
        }
        else
        {
            LageZeichen.Art = f is null ? "unbekannt" : "warn";
            LageText.Text = f is null ? "Warte auf Daten von Mars …" : $"Seit {age / 1000:F0} s keine Daten von Mars.";
            LageMeta.Text = f is null ? "Mars eingeschaltet und im Netz? Path Recorder darf parallel laufen."
                                      : "Netzwerkkabel und Mars prüfen. Die Aufnahme läuft weiter, sobald Daten kommen.";
        }

        if (f is not null)
        {
            TcText.Text = f.Tc.ToString();
            TcText.Foreground = connected ? Theme.Get("Text") : Leise;
            UpdateDevices(f, connected);
        }
        GeraeteZahl.Text = _devices.Count.ToString(CultureInfo.InvariantCulture);

        lock (monitor.Events)
        {
            if (_shownEvents > monitor.Events.Count) { _events.Clear(); _shownEvents = 0; }
            for (; _shownEvents < monitor.Events.Count; _shownEvents++) _events.Insert(0, EventRow.From(monitor.Events[_shownEvents]));
        }
        EreignisZahl.Text = _events.Count.ToString(CultureInfo.InvariantCulture);
        EreignisLeer.Visibility = _events.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    string DeviceName(MarsDevice d)
    {
        if (d.IsBaseStation) return "Basisstation";
        var rover = _status?.Rovers.FirstOrDefault(r => r.Serial == d.Serial);
        string role = KnownRoles.TryGetValue(d.Serial, out var r2) ? " · " + r2 : "";
        return rover is not null && rover.Port is >= 0 and < 4 ? $"Rover {rover.RoverNumber}{role}" : "Tracker" + role;
    }

    // ---------- Ablage auf dem NAS (08_TRACKING im Drehordner) ----------

    async Task AblageAbarbeiten()
    {
        if (_ablageLaeuft) return;
        _ablageLaeuft = true;
        try
        {
            var (ort, fertig) = await Task.Run(() => (Ablage.Finden(), _ablage.Abarbeiten()));
            _ablageOrt = ort;
            foreach (var a in fertig)
                if (_recorder is null) { TakeStatus.Text = $"abgelegt: {a.AbgelegtIn}"; TakeStatus.Foreground = Theme.Get("Text2"); }
        }
        finally { _ablageLaeuft = false; }
        UpdateMarsRows();
    }

    // ---------- Updates ----------

    async Task UpdatePruefen()
    {
        await _updater.PruefenAsync();
        UpdateText.Text = _updater.BereitVersion is null ? _updater.Zustand : $"{_updater.Zustand} · spielt sich nachts ein (03–05 Uhr, ohne Aufnahme)";
        KopfVersion.Text = $"v{_updater.Version} · {(_updater.BereitVersion is null ? _updater.Zustand : $"Update {_updater.BereitVersion} bereit")}";
        KopfVersion.Foreground = _updater.BereitVersion is null ? Leise : Theme.Get("AkzentText");
        UpdateText.Foreground = _updater.BereitVersion is null ? Leise : Theme.Get("AkzentText");
        UpdateButton.Visibility = _updater.BereitVersion is null ? Visibility.Collapsed : Visibility.Visible;
        UpdateButton.IsEnabled = _recorder is null;
    }

    /// <summary>Nachts (03–05 Uhr) ein geladenes Update von selbst einspielen, wenn Ruhe ist (Core: NachtUpdate).</summary>
    void NachtsEinspielen()
    {
        if (_settings.ReadOnly) return;   // nie im Prüfbild
        if (!NachtUpdate.Jetzt(DateTime.Now, _updater.BereitVersion is not null, _recorder is not null,
                _stage?.Laufend is not null, _ablage.Offen.Count, _letzterTakeLokal)) return;
        _nachtTimer.Stop();
        _client?.Dispose();   // bei Mars abmelden
        _remote?.Dispose();
        _stage?.Dispose();
        _updater.JetztNeuStarten();   // spielt ein und startet die App neu
    }

    void OnUpdateRestart(object sender, RoutedEventArgs e)
    {
        if (_recorder is not null) { MessageBox.Show(this, "Zuerst die Aufnahme stoppen."); return; }
        _client?.Dispose();   // bei Mars abmelden, bevor die App beendet wird
        _updater.JetztNeuStarten();
    }

    // ---------- Stage (VP Companion App): REC/Cut der Klappe ----------

    void ConnectStage()
    {
        _stage?.Dispose();
        _stage = new StageListener(_settings.StageHost, Environment.MachineName);
        _stage.ConnectionChanged += _ => Dispatcher.BeginInvoke(UpdateMarsRows);
        _stage.TakeStarted += t => Dispatcher.BeginInvoke(() => OnStageStart(t));
        _stage.TakeStopped += id => Dispatcher.BeginInvoke(() => OnStageStop(id));
    }

    void OnStageStart(StageTake t)
    {
        if (KlappeBox.IsChecked != true) return;
        if (_recorder is not null)
        {
            if (_ausloeser == "Klappe") StopTake();   // neuer Take ohne Cut dazwischen
            else return;                              // laufende Hand-Aufnahme nicht anfassen
        }
        _stageTake = t;
        TakeBox.Text = t.Name;
        StartTake("Klappe");
    }

    async void OnStageStop(string id)
    {
        if (_recorder is null || _ausloeser != "Klappe" || _stageTake?.Id != id) return;
        await Task.Delay(1000);   // Nachlauf 1 s
        if (_recorder is not null && _ausloeser == "Klappe" && _stageTake?.Id == id) StopTake();
    }

    void OnKlappeChanged(object sender, RoutedEventArgs e)
    {
        _settings.MitKlappe = KlappeBox.IsChecked == true;
        _settings.Save();
        if (IsLoaded) UpdateMarsRows();
    }

    // ---------- Mars-Web-Schnittstelle (Port 7008) ----------

    async Task PollRemote()
    {
        var remote = _remote;
        if (remote is null || _polling) return;
        _polling = true;
        try { _status = await remote.ReadAsync(); _remoteError = null; }
        catch (Exception ex) { _remoteError = ex is TaskCanceledException ? "antwortet nicht" : ex.Message; }
        finally { _polling = false; }
        // Die erste Abfrage startet im Konstruktor; ihre Fortsetzung kann ausserhalb des UI-Threads laufen.
        if (remote == _remote) Dispatcher.Invoke(UpdateMarsRows);
    }

    void UpdateMarsRows()
    {
        var st = _status;
        // Web antwortet nicht: letzter Stand bleibt stehen, aber ohne Bewertung (gestrichelter Ring)
        bool alt = _remoteError is not null;
        MarsWebHinweis.Text = !alt ? "Web-Schnittstelle :7008" : st is null ? $"Web-Schnittstelle {_remoteError}" : $"{_remoteError} · letzter Stand";
        _mars.Clear();
        void Add(string name, string value, string art) =>
            _mars.Add(new StatusRow(name, value, art, art == "unbekannt" ? Leise : Theme.Get("Text")));
        string Bewertet(bool ok) => alt ? "unbekannt" : ok ? "ok" : "warn";

        bool stageOk = _stage?.Connected == true;
        bool klappe = KlappeBox.IsChecked == true;
        Add("Klappe (Stage)", !stageOk ? "nicht verbunden" : klappe ? $"steuert · {_stage!.Einstellung ?? "keine Einstellung"}" : "verbunden · aus",
            stageOk ? "ok" : klappe ? "warn" : "unbekannt");
        int offen = _ablage.Offen.Count;
        string? fehler = _ablage.Offen.Select(a => a.Fehler).FirstOrDefault(x => x is not null);
        Add("Ablage (NAS)", _ablageOrt is null ? "nicht gefunden" : offen > 0 ? $"{offen} Take(s) offen" : $"bereit · {_ablageOrt}",
            _ablageOrt is not null && offen == 0 ? "ok" : "warn");
        if (offen > 0 && fehler is not null) Add("", fehler, "warn");
        var mon = _takeMonitor ?? _live;
        string fs = mon.FilterSchaetzung;
        Add("Mars-Filter (aus Daten)", fs == "unbekannt" ? "unbekannt · zu wenig Ruhe" : $"{fs} · {mon.FilterAnteilGleich:P0} gleich",
            fs == "unbekannt" ? "unbekannt" : fs == "aus" ? "ok" : "warn");
        if (st is null)
        {
            if (alt) Add("Web-Schnittstelle", _remoteError!, "unbekannt");
            return;
        }
        Add("Timecode-Eingang", st.TimecodeEnabled ? $"an · {st.Timecode}" : "kein Signal", Bewertet(st.TimecodeEnabled));
        Add("Genlock", st.GenlockEnabled ? "synchron" : "aus", Bewertet(st.GenlockEnabled));
        Add("Basisstationen online", st.BaseStations.ToString(CultureInfo.InvariantCulture), Bewertet(st.BaseStations >= 2));
        foreach (var r in st.Rovers)
            Add($"Rover {r.RoverNumber}", r.StateText, Bewertet(r.PoseValid));
    }

    void UpdateDevices(MarsFrame f, bool connected)
    {
        var seen = new HashSet<string>();
        foreach (var d in f.Devices.OrderBy(d => d.IsBaseStation).ThenBy(d => d.Serial))
        {
            seen.Add(d.Serial);
            var row = _devices.FirstOrDefault(r => r.Serial == d.Serial);
            if (row is null)
            {
                row = new DeviceRow { Serial = d.Serial };
                int index = d.IsBaseStation ? _devices.Count : _devices.TakeWhile(r => !r.IsBase).Count();
                _devices.Insert(index, row);
            }
            row.IsBase = d.IsBaseStation;
            row.Name = DeviceName(d);
            row.Position = string.Format(CultureInfo.InvariantCulture, "{0,7:F3}  {1,7:F3}  {2,7:F3} m", d.X, d.Y, d.Z);
            if (d.IsBaseStation)
            {
                double drift = (_takeMonitor ?? _live).BaseStationDriftMm(d.Serial);
                bool ruhig = drift < 2;
                row.Info = string.Format(CultureInfo.InvariantCulture, "Drift {0:F1} mm", drift);
                row.InfoBrush = ruhig ? Leise : Warn;
                row.State = !connected ? "letzter Stand" : ruhig ? "steht fest" : "verschoben – Kartensprung?";
                row.Art = !connected ? "unbekannt" : ruhig ? "ok" : "warn";
            }
            else
            {
                row.Info = "";
                row.State = connected ? "wird verfolgt" : "letzter Stand";
                row.Art = connected ? "ok" : "unbekannt";
            }
        }
        foreach (var r in _devices.Where(r => !seen.Contains(r.Serial)))
        {
            r.State = "fehlt im Datenstrom";
            r.Art = "warn";
        }
    }

    // ---------- Aufnahme ----------

    void OnRec(object sender, RoutedEventArgs e)
    {
        if (_haltenGestoppt) { _haltenGestoppt = false; return; }   // Loslassen nach dem Halten startet nichts Neues
        if (_recorder is null) { _stageTake = null; StartTake("Hand"); return; }
        if (!_halten.IsEnabled) HaltenHinweis();
    }

    void OnRecDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { _haltenGestoppt = false; HaltenStart(); }
    void OnRecUp(object sender, System.Windows.Input.MouseButtonEventArgs e) => HaltenAbbrechen();
    void OnRecLeave(object sender, System.Windows.Input.MouseEventArgs e) => HaltenAbbrechen();

    void OnRecKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is not (System.Windows.Input.Key.Space or System.Windows.Input.Key.Enter)) return;
        if (e.IsRepeat) { if (_recorder is not null) e.Handled = true; return; }
        _haltenGestoppt = false;
        if (_recorder is null) return;   // Starten bleibt ein Tipp
        HaltenStart();
        e.Handled = true;                // Enter würde sonst sofort klicken
    }

    void OnRecKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is not (System.Windows.Input.Key.Space or System.Windows.Input.Key.Enter) || !_halten.IsEnabled) return;
        HaltenAbbrechen();
        e.Handled = true;
    }

    void HaltenStart()
    {
        if (_recorder is null || _halten.IsEnabled) return;
        _haltenUhr.Restart();
        _halten.Start();
    }

    void HaltenTick()
    {
        double p = _haltenUhr.Elapsed.TotalSeconds / HaltenSek;
        RecButton.Tag = Math.Min(1.0, p);
        if (p < 1) return;
        _halten.Stop();
        RecButton.Tag = 0.0;
        _haltenGestoppt = true;
        if (_recorder is not null) StopTake();
    }

    void HaltenAbbrechen()
    {
        if (!_halten.IsEnabled) return;
        _halten.Stop();
        RecButton.Tag = 0.0;
        HaltenHinweis();
    }

    void HaltenHinweis()
    {
        if (_recorder is null) return;
        TakeStatus.Text = "Zum Stoppen 0,6 s gedrückt halten";
        TakeStatus.Foreground = Theme.Get("Text2");
    }

    void StartTake(string ausloeser)
    {
        if (_latest is null) { MessageBox.Show(this, "Keine Daten von Mars — Aufnahme nicht möglich."); return; }
        string folder = FolderBox.Text.Trim();
        try { Directory.CreateDirectory(folder); }
        catch (Exception ex) { MessageBox.Show(this, "Speicherort geht nicht: " + ex.Message); return; }

        _takeName = TakeNames.Safe(TakeBox.Text);
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        _takeCsv = Path.Combine(folder, $"{_takeName}_{stamp}.csv");
        try
        {
            var rec = new MarsRecorder(_takeCsv);
            lock (_lock)
            {
                _takeMonitor = new MarsMonitor();
                _vorlaufBilder = _vorlauf.Count;
                foreach (var v in _vorlauf) { rec.Write(v); _takeMonitor.Add(v.Frame); }
                _vorlauf.Clear();
                _recorder = rec;
                _startTc = _latest?.Tc.ToString();
            }
            _ausloeser = ausloeser;
        UpdateButton.IsEnabled = false;
        }
        catch (Exception ex) { MessageBox.Show(this, "Datei geht nicht: " + ex.Message); return; }

        _takeStartUtc = DateTime.UtcNow;
        _takeStartLokal = DateTime.Now;
        _events.Clear();
        _shownEvents = 0;
        RecText.Text = "Aufnahme stoppen · halten";
        RecButton.SetResourceReference(BackgroundProperty, "RotFlaeche");
        RecButton.SetResourceReference(BorderBrushProperty, "RotFlaeche");
        RecText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "AufRot");
        RecDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "AufRot");
        TakeStatus.Text = ausloeser == "Klappe" ? $"gestartet von der Klappe · Stage-Take {_stageTake?.Id}" : " ";
        TakeBox.IsEnabled = FolderBox.IsEnabled = FolderButton.IsEnabled = IpBox.IsEnabled = ReconnectButton.IsEnabled = false;
        _settings.Folder = folder;
        _settings.Save();
    }

    void StopTake()
    {
        MarsRecorder? rec;
        MarsMonitor? mon;
        MarsFrame? last;
        lock (_lock)
        {
            rec = _recorder; mon = _takeMonitor; last = _latest;
            _recorder = null; _takeMonitor = null;
        }
        rec?.Dispose();
        _letzterTakeLokal = DateTime.Now;
        RecDot.Opacity = 1;
        RecText.Text = "Aufnahme starten";
        RecButton.ClearValue(BackgroundProperty);
        RecButton.ClearValue(BorderBrushProperty);
        _halten.Stop();
        RecButton.Tag = 0.0;
        RecText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "AufAkzent");
        RecDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "AufAkzent");
        TakeBox.IsEnabled = FolderBox.IsEnabled = FolderButton.IsEnabled = IpBox.IsEnabled = ReconnectButton.IsEnabled = true;
        UpdateMarsRows();

        if (mon is not null && _takeCsv is not null)
        {
            List<string> events;
            lock (mon.Events) events = mon.Events.ToList();
            var info = new TakeInfo(_takeName!, Path.GetFileName(_takeCsv), _takeStartUtc, DateTime.UtcNow,
                _startTc ?? "", last?.Tc.ToString() ?? "", last?.Fps ?? 0, mon.Frames, mon.MissingFrames, mon.Jumps,
                mon.BaseShifts, last?.Devices.Select(d => d.Serial).ToList() ?? new List<string>(), events,
                StageTakeId: _stageTake?.Id, StageStartTc: _stageTake?.StartTc, VorlaufBilder: _vorlaufBilder, Ausloeser: _ausloeser,
                Filter: new MarsFilter(mon.FilterSchaetzung, Math.Round(mon.FilterAnteilGleich, 3), mon.RuhigeBilder));
            try { info.Save(Path.ChangeExtension(_takeCsv, ".json")); } catch { /* CSV ist das Wichtige */ }
            // Klappen-Takes mit Projekt aufs NAS (Systemkarte: 08_TRACKING im Drehordner); Hand-Takes bleiben lokal.
            if (_ausloeser == "Klappe" && _stageTake is { Projekt: { Length: > 0 } projekt } st)
            {
                _ablage.Add(new AblageAuftraege.Auftrag(_takeCsv, projekt, _takeStartLokal, Ablage.Stamm(st.Einstellung, st.Nummer, st.Id)));
                _ = AblageAbarbeiten();
            }

            bool clean = mon.MissingFrames + mon.Jumps + mon.BaseShifts == 0;
            TakeStatus.Text = $"{_takeName} gespeichert · {info.StartTimecode} – {info.EndTimecode} · {mon.Frames} Bilder" +
                              (clean ? " · ohne Störung" : $" · {mon.MissingFrames + mon.Jumps + mon.BaseShifts} Störungen");
            TakeStatus.Foreground = clean ? Theme.Get("Text2") : Warn;
        }
        UpdateButton.IsEnabled = true;
        if (_ausloeser == "Hand") TakeBox.Text = TakeNames.Next(TakeBox.Text);
        _stageTake = null;
        _settings.NextTake = TakeBox.Text;
        _settings.Save();
    }

    // Nur für Prüfbilder (App --bild … --rec)
    internal void StartForPreview()
    {
        _settings.ReadOnly = true;
        FolderBox.Text = Path.Combine(Path.GetTempPath(), "TrackingRecorder-Vorschau");
        StartTake("Hand");
    }

    internal void StopForPreview()
    {
        string? csv = _takeCsv;
        StopTake();
        try { if (csv is not null) foreach (var f in Directory.GetFiles(Path.GetDirectoryName(csv)!)) File.Delete(f); } catch { }
    }

    /// <summary>Verbindung lösen ohne Fenster (Prüfbild).</summary>
    internal bool HasMarsStatus => _status is not null;

    internal void Shutdown() { _client?.Dispose(); _remote?.Dispose(); _stage?.Dispose(); }

    void OnPickFolder(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { InitialDirectory = FolderBox.Text };
        if (dlg.ShowDialog(this) == true) FolderBox.Text = dlg.FolderName;
    }

    void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_recorder is not null)
        {
            if (MessageBox.Show(this, "Aufnahme läuft. Stoppen und beenden?", "TrackingRecorder",
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes) { e.Cancel = true; return; }
            StopTake();
        }
        _client?.Dispose();   // meldet sich bei Mars ab
        _remote?.Dispose();
        _stage?.Dispose();
        _updater.BeimBeendenEinspielen();
    }
}

/// <summary>Zeile im Mars-Block; Art = Zeichen (ok, warn, unbekannt).</summary>
public sealed record StatusRow(string Name, string Value, string Art, Brush ValueBrush);

/// <summary>Ereignis aus dem Monitor, „[hh:mm:ss:ff] Text" getrennt in Timecode und Satz.</summary>
public sealed record EventRow(string Tc, string Text)
{
    public static EventRow From(string line)
    {
        int close = line.IndexOf(']');
        return line.StartsWith('[') && close > 0 ? new EventRow(line[1..close], line[(close + 1)..].Trim()) : new EventRow("", line);
    }
}

public sealed class DeviceRow : INotifyPropertyChanged
{
    string _name = "", _position = "", _info = "", _state = "";
    string _art = "unbekannt";
    Brush _infoBrush = Brushes.Gray;
    public string Serial { get; init; } = "";
    public bool IsBase { get; set; }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Position { get => _position; set => Set(ref _position, value); }
    public string Info { get => _info; set => Set(ref _info, value); }
    public string State { get => _state; set => Set(ref _state, value); }
    /// <summary>Zeichen: ok = verfolgt/steht fest, warn = Störung, unbekannt = letzter Stand.</summary>
    public string Art { get => _art; set => Set(ref _art, value); }
    public Brush InfoBrush { get => _infoBrush; set => Set(ref _infoBrush, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>Einstellungen pro Benutzer in %LOCALAPPDATA%\TrackingRecorder\settings.json.</summary>
public sealed class Settings
{
    public string MarsIp { get; set; } = "192.168.100.60";
    public string Folder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackingRecorder", "Takes");
    public string NextTake { get; set; } = "Take_001";
    public bool MitKlappe { get; set; } = true;
    public string StageHost { get; set; } = "192.168.100.20";

    /// <summary>Für Prüfbilder: nichts auf die Platte schreiben.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public bool ReadOnly { get; set; }

    // Roaming statt Local: %LOCALAPPDATA%\TrackingRecorder ist der Programmordner von Velopack (wird bei Deinstallation gelöscht).
    static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrackingRecorder", "settings.json");
    static string AlterPfad => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrackingRecorder", "settings.json");

    public static Settings Load()
    {
        foreach (var p in new[] { FilePath, AlterPfad })
        {
            try { if (File.Exists(p)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(p)) ?? new Settings(); }
            catch { }
        }
        return new Settings();
    }

    public void Save()
    {
        if (ReadOnly) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch { /* Einstellungen sind Komfort */ }
    }
}
