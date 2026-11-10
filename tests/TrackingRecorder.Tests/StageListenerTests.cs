using System.Text;
using TrackingRecorder.Core;

namespace TrackingRecorder.Tests;

public class StageListenerTests
{
    static byte[] Sitzung(bool laeuft, string? take = null) => Encoding.UTF8.GetBytes(
        "{\"v\":1,\"art\":\"ereignis\",\"id\":\"x\",\"zeit\":\"t\",\"typ\":\"sitzung\",\"daten\":{" +
        "\"projekt\":{\"id\":\"p\",\"name\":\"Verkehrshaus\",\"kurzname\":\"VKH\"}," +
        "\"aktiveEinstellung\":{\"id\":\"e\",\"name\":\"Sz12\"},\"takeLaeuft\":" + (laeuft ? "true" : "false") + "," +
        (take is null ? "\"laufenderTake\":null" : "\"laufenderTake\":{\"id\":\"" + take + "\",\"nummer\":3,\"startTc\":\"14:05:12:07\",\"startZeit\":\"z\"}") + "}}");

    [Fact]
    public void StartAndStopFromSitzung()
    {
        using var l = new StageListener("127.0.0.1", "test", port: 1);   // verbindet nicht, nur Handle()
        StageTake? started = null; string? stopped = null;
        l.TakeStarted += t => started = t;
        l.TakeStopped += id => stopped = id;

        l.Handle(Sitzung(false));
        Assert.Null(started);
        l.Handle(Sitzung(true, "01KTAKE"));
        Assert.Equal("01KTAKE", started!.Id);
        Assert.Equal("VKH_Sz12_T03", started.Name);
        Assert.Equal("14:05:12:07", started.StartTc);
        l.Handle(Sitzung(true, "01KTAKE"));   // Wiederholung: kein zweiter Start
        l.Handle(Sitzung(false));
        Assert.Equal("01KTAKE", stopped);
    }

    [Fact]
    public void IgnoresOtherMessages()
    {
        using var l = new StageListener("127.0.0.1", "test", port: 1);
        bool any = false;
        l.TakeStarted += _ => any = true;
        l.Handle(Encoding.UTF8.GetBytes("{\"art\":\"ereignis\",\"typ\":\"kamera\",\"daten\":{\"takeLaeuft\":true}}"));
        l.Handle(Encoding.UTF8.GetBytes("kein json"));
        Assert.False(any);
    }
}
