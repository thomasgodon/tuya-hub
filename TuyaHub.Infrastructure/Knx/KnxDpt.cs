namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// Encoders for the KNX datapoint types used on the Tuya → KNX feedback path. Each returns the raw
/// group-value payload in <b>KNX wire order</b> (big-endian), ready to hand straight to a
/// <c>GroupValue</c> — no further byte reversal. (DsmrHub achieved the same result by reversing
/// little-endian <c>BitConverter</c> output at write time; here the order is settled once, in this
/// single tested place.)
/// </summary>
internal static class KnxDpt
{
    /// <summary>DPT 1.001 (switch): a single byte, 1 = true / 0 = false.</summary>
    public static byte[] Bool(bool value) => [(byte)(value ? 1 : 0)];

    /// <summary>DPT 5.010 (counter, 0..255): a single byte carrying the value verbatim (fan speed 0..6).</summary>
    public static byte[] Count(int value) => [(byte)Math.Clamp(value, 0, 255)];

    /// <summary>
    /// DPT 5.001 (scaling, 0..100 %): a single byte scaled to 0..255, <c>round(pct * 255 / 100)</c>.
    /// </summary>
    public static byte[] Percent(int percent)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        var scaled = (int)Math.Round(clamped * 255 / 100.0, MidpointRounding.AwayFromZero);
        return [(byte)scaled];
    }

    /// <summary>
    /// DPT 7.006 (2-byte unsigned, minutes): two bytes, big-endian high byte first.
    /// </summary>
    public static byte[] Minutes(int minutes)
    {
        var value = (ushort)Math.Clamp(minutes, 0, ushort.MaxValue);
        return [(byte)(value >> 8), (byte)(value & 0xFF)];
    }
}
