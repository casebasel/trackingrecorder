using TrackingRecorder.Core;

namespace TrackingRecorder.Tests;

public class MarsProtocolTests
{
    static byte[][] Samples() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "mars_frame_sample.hex"))
            .Where(l => l.Length > 0).Select(Convert.FromHexString).ToArray();

    [Fact]
    public void ParsesRealPathRecorderFrame()
    {
        Assert.True(MarsProtocol.TryParse(Samples()[0], out var f));
        Assert.Equal("15:24:20:11", f!.Tc.ToString());
        Assert.Equal(25f, f.Fps);
        Assert.Equal(6, f.Devices.Count);
        Assert.Equal(2, f.Devices.Count(d => !d.IsBaseStation));
        Assert.Equal(4, f.Devices.Count(d => d.IsBaseStation));

        var cam = f.Devices.Single(d => d.Serial == "LHR-0FFCBEE6");
        Assert.InRange(cam.Y, 1.5, 1.8);   // Kamera auf ~1,65 m
        double norm = Math.Sqrt(cam.Qx * cam.Qx + cam.Qy * cam.Qy + cam.Qz * cam.Qz + cam.Qw * cam.Qw);
        Assert.Equal(1.0, norm, 6);
    }

    [Fact]
    public void ConsecutiveFramesAre40msApart()
    {
        var s = Samples();
        MarsProtocol.TryParse(s[0], out var a);
        MarsProtocol.TryParse(s[1], out var b);
        Assert.Equal(40.0, b!.DeviceTimeMs - a!.DeviceTimeMs, 0);
        Assert.Equal("15:24:20:12", b.Tc.ToString());
    }

    [Fact]
    public void RejectsGarbage() => Assert.False(MarsProtocol.TryParse(new byte[] { 1, 2, 3 }, out _));

    [Fact]
    public void MonitorFlagsBaseStationShiftAndJump()
    {
        MarsProtocol.TryParse(Samples()[0], out var f);
        var m = new MarsMonitor();
        m.Add(f!);
        var moved = f!.Devices.Select(d => d.IsBaseStation && d.Serial == "LHB-2211DE29" ? d with { X = d.X + 0.02 }
                                         : !d.IsBaseStation && d.Serial == "LHR-16D49F61" ? d with { Y = d.Y + 0.5 } : d).ToList();
        m.Add(f with { DeviceTimeMs = f.DeviceTimeMs + 40, Devices = moved });
        Assert.Equal(1, m.BaseShifts);
        Assert.Equal(1, m.Jumps);
        Assert.Equal(0, m.MissingFrames);
    }
}
