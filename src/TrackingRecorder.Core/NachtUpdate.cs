namespace TrackingRecorder.Core;

/// <summary>
/// Wann die App ein geladenes Update von selbst einspielen und neu starten darf (Marlon 15.11.2026):
/// nur nachts zwischen 03:00 und 05:00 Ortszeit, nie während einer Aufnahme oder eines laufenden Stage-Takes,
/// nicht solange noch Takes aufs NAS warten, und erst wenn seit dem letzten Take mindestens 60 Minuten Ruhe ist.
/// </summary>
public static class NachtUpdate
{
    public static readonly TimeSpan FensterVon = TimeSpan.FromHours(3);
    public static readonly TimeSpan FensterBis = TimeSpan.FromHours(5);
    public static readonly TimeSpan Ruhe = TimeSpan.FromMinutes(60);

    public static bool Jetzt(DateTime lokal, bool updateBereit, bool aufnahme, bool stageTakeLaeuft,
                             int ablageOffen, DateTime? letzterTakeLokal)
    {
        if (!updateBereit || aufnahme || stageTakeLaeuft || ablageOffen > 0) return false;
        var t = lokal.TimeOfDay;
        if (t < FensterVon || t >= FensterBis) return false;
        return letzterTakeLokal is null || lokal - letzterTakeLokal.Value >= Ruhe;
    }
}
