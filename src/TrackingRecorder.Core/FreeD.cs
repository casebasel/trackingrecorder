namespace TrackingRecorder.Core;

/// <summary>Eine Kamerapose aus einem FreeD-D1-Paket (29 Bytes).</summary>
public readonly record struct FreeDPose(
    byte CameraId,
    double PanDeg, double TiltDeg, double RollDeg,
    double XMm, double YMm, double ZMm,
    int Zoom, int Focus,
    ushort Spare);

public static class FreeD
{
    public const int D1Length = 29;
    public const byte D1Type = 0xD1;

    /// <summary>
    /// Liest ein FreeD-D1-Paket. Winkel: 24 Bit signed / 32768 = Grad.
    /// Positionen: 24 Bit signed / 64 = mm. Zoom/Fokus: 24 Bit unsigned, Rohwert.
    /// Die zwei "Spare"-Bytes nutzen manche Hersteller für Zähler oder Timecode —
    /// wir behalten sie roh, bis wir wissen, was Mars dort hineinschreibt.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> data, out FreeDPose pose)
    {
        pose = default;
        if (data.Length < D1Length || data[0] != D1Type) return false;
        if (Checksum(data[..28]) != data[28]) return false;

        pose = new FreeDPose(
            CameraId: data[1],
            PanDeg: S24(data[2..]) / 32768.0,
            TiltDeg: S24(data[5..]) / 32768.0,
            RollDeg: S24(data[8..]) / 32768.0,
            XMm: S24(data[11..]) / 64.0,
            YMm: S24(data[14..]) / 64.0,
            ZMm: S24(data[17..]) / 64.0,
            Zoom: U24(data[20..]),
            Focus: U24(data[23..]),
            Spare: (ushort)((data[26] << 8) | data[27]));
        return true;
    }

    public static byte Checksum(ReadOnlySpan<byte> first28)
    {
        int sum = 0x40;
        foreach (var b in first28) sum -= b;
        return (byte)(sum & 0xFF);
    }

    /// <summary>Baut ein D1-Paket — für Tests und für späteres Weitersenden.</summary>
    public static byte[] Build(FreeDPose p)
    {
        var d = new byte[D1Length];
        d[0] = D1Type;
        d[1] = p.CameraId;
        W24(d, 2, (int)Math.Round(p.PanDeg * 32768));
        W24(d, 5, (int)Math.Round(p.TiltDeg * 32768));
        W24(d, 8, (int)Math.Round(p.RollDeg * 32768));
        W24(d, 11, (int)Math.Round(p.XMm * 64));
        W24(d, 14, (int)Math.Round(p.YMm * 64));
        W24(d, 17, (int)Math.Round(p.ZMm * 64));
        W24(d, 20, p.Zoom);
        W24(d, 23, p.Focus);
        d[26] = (byte)(p.Spare >> 8);
        d[27] = (byte)p.Spare;
        d[28] = Checksum(d.AsSpan(0, 28));
        return d;
    }

    static int U24(ReadOnlySpan<byte> b) => (b[0] << 16) | (b[1] << 8) | b[2];
    static int S24(ReadOnlySpan<byte> b) { int v = U24(b); return (v & 0x800000) != 0 ? v - 0x1000000 : v; }
    static void W24(byte[] d, int o, int v) { d[o] = (byte)(v >> 16); d[o + 1] = (byte)(v >> 8); d[o + 2] = (byte)v; }
}
