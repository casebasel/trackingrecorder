using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace TrackingRecorder.Core;

/// <summary>Take, den die Stage (Klappe / iOS-App / Kamera-REC) gerade aufnimmt.</summary>
public sealed record StageTake(string Id, int Nummer, string? StartTc, string? Einstellung, string? Projekt)
{
    /// <summary>Dateiname-tauglicher Take-Name, z. B. „PROJ_Einstellung_T03".</summary>
    public string Name => TakeNames.Safe(string.Join("_",
        new[] { Projekt, Einstellung, $"T{Nummer:00}" }.Where(s => !string.IsNullOrWhiteSpace(s))));
}

/// <summary>
/// Hört beim Stage-Server der VP Companion App mit (nur lesen, keine Anmeldung):
/// ws://&lt;stage&gt;:4400/client?quelle=trackingrecorder&amp;geraet=…&amp;schmal=1.
/// Massgebend ist das Ereignis „sitzung" (SitzungDaten in vp-companion-app packages/shared/src/protokoll.ts):
/// takeLaeuft false → true = REC-Start mit laufenderTake {id, nummer, startTc}; true → false = Stop.
/// Verbindet sich bei Abbruch selbst neu (alle 3 s).
/// </summary>
public sealed class StageListener : IDisposable
{
    readonly Uri _uri;
    readonly CancellationTokenSource _cts = new();
    bool _takeLaeuft;
    string? _laufendeId;

    public event Action<StageTake>? TakeStarted;
    public event Action<string>? TakeStopped;        // Take-ID
    public event Action<bool>? ConnectionChanged;

    public bool Connected { get; private set; }
    public StageTake? Laufend { get; private set; }
    public string? NaechsterTakeId { get; private set; }
    public string? Einstellung { get; private set; }

    public StageListener(string host, string geraet, int port = 4400)
    {
        _uri = new Uri($"ws://{host}:{port}/client?quelle=trackingrecorder&geraet={Uri.EscapeDataString(geraet)}&schmal=1");
        _ = Task.Run(Loop);
    }

    async Task Loop()
    {
        var buf = new byte[1 << 16];
        while (!_cts.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            try
            {
                using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                connectTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                await ws.ConnectAsync(_uri, connectTimeout.Token);
                SetConnected(true);
                var msg = new MemoryStream();
                while (ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    var r = await ws.ReceiveAsync(buf, _cts.Token);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    msg.Write(buf, 0, r.Count);
                    if (!r.EndOfMessage) continue;
                    Handle(msg.ToArray());
                    msg.SetLength(0);
                }
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { return; }
            catch { /* neu verbinden */ }
            SetConnected(false);
            try { await Task.Delay(3000, _cts.Token); } catch { return; }
        }
    }

    void SetConnected(bool c)
    {
        if (Connected == c) return;
        Connected = c;
        ConnectionChanged?.Invoke(c);
    }

    /// <summary>Öffentlich für Tests: verarbeitet eine Nachricht des Servers.</summary>
    public void Handle(byte[] utf8)
    {
        try
        {
            using var doc = JsonDocument.Parse(utf8);
            var root = doc.RootElement;
            if (Str(root, "art") != "ereignis" || Str(root, "typ") != "sitzung") return;
            if (!root.TryGetProperty("daten", out var d)) return;

            bool laeuft = d.TryGetProperty("takeLaeuft", out var tl) && tl.ValueKind == JsonValueKind.True;
            NaechsterTakeId = Str(d, "naechsterTakeId");
            Einstellung = d.TryGetProperty("aktiveEinstellung", out var e) && e.ValueKind == JsonValueKind.Object ? Str(e, "name") : null;
            string? projekt = d.TryGetProperty("projekt", out var p) && p.ValueKind == JsonValueKind.Object ? Str(p, "kurzname") ?? Str(p, "name") : null;

            if (laeuft && d.TryGetProperty("laufenderTake", out var lt) && lt.ValueKind == JsonValueKind.Object)
            {
                string id = Str(lt, "id") ?? "";
                if (!_takeLaeuft || id != _laufendeId)
                {
                    if (_takeLaeuft && _laufendeId is not null) TakeStopped?.Invoke(_laufendeId);
                    _takeLaeuft = true;
                    _laufendeId = id;
                    int nummer = lt.TryGetProperty("nummer", out var n) && n.TryGetInt32(out var nn) ? nn : 0;
                    Laufend = new StageTake(id, nummer, Str(lt, "startTc"), Einstellung, projekt);
                    TakeStarted?.Invoke(Laufend);
                }
            }
            else if (!laeuft && _takeLaeuft)
            {
                _takeLaeuft = false;
                var id = _laufendeId ?? "";
                _laufendeId = null;
                Laufend = null;
                TakeStopped?.Invoke(id);
            }
        }
        catch (JsonException) { }
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public void Dispose() => _cts.Cancel();
}
