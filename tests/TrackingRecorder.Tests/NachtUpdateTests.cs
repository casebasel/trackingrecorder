using TrackingRecorder.Core;
using Xunit;

namespace TrackingRecorder.Tests;

public class NachtUpdateTests
{
    static readonly DateTime Nacht = new(2026, 11, 16, 3, 30, 0);

    [Fact] public void NachtsOhneTakeJa() => Assert.True(NachtUpdate.Jetzt(Nacht, true, false, false, 0, null));
    [Fact] public void OhneUpdateNein() => Assert.False(NachtUpdate.Jetzt(Nacht, false, false, false, 0, null));
    [Fact] public void TagsNein() => Assert.False(NachtUpdate.Jetzt(Nacht.AddHours(10), true, false, false, 0, null));
    [Fact] public void FuenfUhrNein() => Assert.False(NachtUpdate.Jetzt(Nacht.Date.AddHours(5), true, false, false, 0, null));
    [Fact] public void AufnahmeNein() => Assert.False(NachtUpdate.Jetzt(Nacht, true, true, false, 0, null));
    [Fact] public void StageTakeNein() => Assert.False(NachtUpdate.Jetzt(Nacht, true, false, true, 0, null));
    [Fact] public void AblageOffenNein() => Assert.False(NachtUpdate.Jetzt(Nacht, true, false, false, 2, null));
    [Fact] public void KurzNachTakeNein() => Assert.False(NachtUpdate.Jetzt(Nacht, true, false, false, 0, Nacht.AddMinutes(-20)));
    [Fact] public void StundeNachTakeJa() => Assert.True(NachtUpdate.Jetzt(Nacht, true, false, false, 0, Nacht.AddMinutes(-61)));
}
