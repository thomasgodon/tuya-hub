namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// Encoders and decoders for the KNX datapoint types used by the bridge. Each side works in
/// <b>KNX wire order</b> (big-endian): encoders return the raw group-value payload ready to hand
/// straight to a <c>GroupValue</c> (feedback path); decoders read the raw payload of an inbound
/// <c>GroupValue</c> back into a domain-shaped value (command path). Keeping both directions in this
/// single tested place settles the byte order once. (DsmrHub achieved the encode side by reversing
/// little-endian <c>BitConverter</c> output at write time.)
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

    /// <summary>DPT 1.001 (switch): true when the low bit of the first payload byte is set.</summary>
    public static bool DecodeBool(byte[] payload) => payload.Length > 0 && (payload[0] & 0x01) != 0;

    /// <summary>
    /// DPT 5.001 (scaling): the inverse of <see cref="Percent"/> — <c>round(byte * 100 / 255)</c>,
    /// yielding 0..100 %.
    /// </summary>
    public static int DecodePercent(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return 0;
        }

        return (int)Math.Round(payload[0] * 100 / 255.0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// DPT 7.006 (2-byte unsigned, minutes): the inverse of <see cref="Minutes"/> — big-endian high
    /// byte first.
    /// </summary>
    public static int DecodeMinutes(byte[] payload)
    {
        if (payload.Length < 2)
        {
            return 0;
        }

        return (payload[0] << 8) | payload[1];
    }

    /// <summary>
    /// DPT 3.007 (dimming control, B1U3): the top bit (0x08) is the direction (1 = up, 0 = down) and
    /// the low 3 bits are the step code. Returns <c>true</c> for a step up, <c>false</c> for a step
    /// down, and <c>null</c> for the break/stop code (step 0), which a discrete 6-level fan ignores
    /// (UC-02). The step width is deliberately not honoured — every telegram is a single ±1 step.
    /// </summary>
    public static bool? DecodeDimStep(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return null;
        }

        if ((payload[0] & 0x07) == 0)
        {
            return null; // Break/stop — no discrete step for this fan.
        }

        return (payload[0] & 0x08) != 0;
    }
}
