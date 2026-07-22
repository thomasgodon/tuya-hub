using Knx.Falcon;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// Encoders and decoders for the KNX datapoint types used by the bridge. Each side works in
/// <b>KNX wire order</b> (big-endian). Encoders return a fully-formed <see cref="GroupValue"/> — the
/// bit size matters: DPT 1.001 must be a <b>1-bit</b> "short" value (<c>new GroupValue(bool)</c>,
/// <c>SizeInBit == 1</c>), not an 8-bit byte array. A boolean wrapped in <c>new GroupValue(byte[])</c>
/// is always 8-bit; actuators tolerate that on a <i>write</i> but a reader discards the malformed
/// oversized <i>response</i>, so boolean <c>GroupValueRead</c>s went unanswered. Decoders read the raw
/// payload of an inbound <c>GroupValue</c> back into a domain-shaped value (command path). Keeping both
/// directions in this single tested place settles size and byte order once.
/// </summary>
internal static class KnxDpt
{
    /// <summary>DPT 1.001 (switch): a 1-bit "short" group value (<c>SizeInBit == 1</c>).</summary>
    public static GroupValue Bool(bool value) => new(value);

    /// <summary>DPT 5.010 (counter, 0..255): a single 8-bit byte carrying the value verbatim (fan speed 0..6).</summary>
    public static GroupValue Count(int value) => new((byte)Math.Clamp(value, 0, 255));

    /// <summary>
    /// DPT 5.001 (scaling, 0..100 %): a single 8-bit byte scaled to 0..255, <c>round(pct * 255 / 100)</c>.
    /// </summary>
    public static GroupValue Percent(int percent)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        var scaled = (int)Math.Round(clamped * 255 / 100.0, MidpointRounding.AwayFromZero);
        return new((byte)scaled);
    }

    /// <summary>
    /// DPT 7.006 (2-byte unsigned, minutes): two bytes, big-endian high byte first.
    /// </summary>
    public static GroupValue Minutes(int minutes)
    {
        var value = (ushort)Math.Clamp(minutes, 0, ushort.MaxValue);
        return new([(byte)(value >> 8), (byte)(value & 0xFF)]);
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
