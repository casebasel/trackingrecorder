using System.Net.Http.Json;

namespace TrackingRecorder.Core;

/// <summary>
/// Web-Schnittstelle „Remote Control" der Mars-Box (http://&lt;mars&gt;:7008, ASP.NET, nur HTTP).
/// Endpunkte aus der Mars-Seite selbst (index + js/site.js, gelesen 10.11.2026).
/// Die App liest nur (GET). Die Seite kann auch schreiben (SetRecenterTarget, SetReinitialize, SetSoftwareFPS);
/// das bleibt bewusst in der Mars-Oberfläche (Marlon 10.11.2026: nur Rover, Basisstationen, Genlock, Timecode).
/// </summary>
public sealed class MarsRemote : IDisposable
{
    public const int Port = 7008;
    readonly HttpClient _http;

    public MarsRemote(string host)
    {
        // Ohne Proxy: die Windows-Proxysuche kostet bei der ersten Abfrage Sekunden, Mars ist immer im LAN.
        _http = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri($"http://{host}:{Port}/"), Timeout = TimeSpan.FromSeconds(4) };
    }

    public sealed record RoverState(string Serial, int Port, int State)
    {
        /// <summary>Port 0/1/2 = Rover 1/2/3 (kabellos ab 4).</summary>
        public int RoverNumber => Port + 1;
        public bool HasEncoder => State == 5;
        public bool PoseValid => State is 4 or 5 or 7;
        public string StateText => State switch
        {
            -1 => "nicht verbunden",
            0 => "startet",
            1 => "SSH verbunden",
            2 => "Fehler",
            3 => "verbunden, keine Pose",
            4 => "Pose gültig",
            5 => "Pose gültig, mit Objektiv-Encoder",
            6 => "kabellos verbunden",
            7 => "kabellos, Pose gültig",
            8 => "kabellos, lädt",
            9 => "kabellos, Akku schwach",
            _ => $"Zustand {State}",
        };
    }

    public sealed record Status(
        bool TimecodeEnabled, string Timecode, bool GenlockEnabled, int BaseStations, IReadOnlyList<RoverState> Rovers);

    sealed record TcDto(bool Enable, string HH, string MM, string SS, string FF);
    sealed record EnableDto(bool Enable);
    sealed record BsDto(int BasestationCount);

    /// <summary>Liest Timecode, Genlock, Basisstationen und Rover gleichzeitig — Mars braucht ~1 s pro Abfrage.</summary>
    public async Task<Status> ReadAsync(CancellationToken ct = default)
    {
        var tcT = _http.GetFromJsonAsync<TcDto>("api/GetTimeCode", ct);
        var glT = _http.GetFromJsonAsync<EnableDto>("api/GetGenlockState", ct);
        var bsT = _http.GetFromJsonAsync<BsDto>("api/GetBasestationsStates", ct);
        var rvT = _http.GetFromJsonAsync<List<RoverState>>("api/GetAllRoverStates", ct);
        await Task.WhenAll(tcT, glT, bsT, rvT);
        var tc = tcT.Result;
        return new Status(
            tc?.Enable ?? false, tc is null ? "" : $"{tc.HH}:{tc.MM}:{tc.SS}:{tc.FF}",
            glT.Result?.Enable ?? false, bsT.Result?.BasestationCount ?? 0,
            (rvT.Result ?? new()).Where(r => !string.IsNullOrEmpty(r.Serial)).ToList());
    }

    public void Dispose() => _http.Dispose();
}
