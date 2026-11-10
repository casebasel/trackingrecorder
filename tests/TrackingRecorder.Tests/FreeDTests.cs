using TrackingRecorder.Core;

namespace TrackingRecorder.Tests;

public class FreeDTests
{
    [Fact]
    public void RoundTrip_KeepsValues()
    {
        var pose = new FreeDPose(2, -123.25, 45.5, -0.75, 1500.5, -2200.25, 1650, 1234, 56789, 0xABCD);
        Assert.True(FreeD.TryParse(FreeD.Build(pose), out var back));
        Assert.Equal(pose, back);
    }

    [Fact]
    public void RejectsBadChecksum()
    {
        var d = FreeD.Build(new FreeDPose(1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        d[28] ^= 0xFF;
        Assert.False(FreeD.TryParse(d, out _));
    }

    [Theory]
    [InlineData(0.0, "00:00:00:00")]
    [InlineData(3661.5, "01:01:01:12")]
    [InlineData(59.99, "00:00:59:24")]
    public void Timecode_From25fps(double seconds, string expected) =>
        Assert.Equal(expected, Timecode.FromTimeOfDay(TimeSpan.FromSeconds(seconds), 25).ToString());
}
