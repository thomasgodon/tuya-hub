namespace TuyaHub.Domain.ValueObjects;

/// <summary>
/// Light brightness (DP 22 <c>bright_value</c>) held on the device scale 0..1000.
/// KNX carries brightness as a 0..100 % value (DPT 5.001); conversion lives here so the
/// scaling rule has one home: <c>dp = round(pct/100*1000)</c>, <c>pct = round(dp/1000*100)</c>.
/// </summary>
public readonly record struct Brightness
{
    public const int MinDp = 0;
    public const int MaxDp = 1000;

    /// <summary>Brightness on the device scale (0..1000).</summary>
    public int Dp { get; }

    private Brightness(int dp) => Dp = dp;

    public static readonly Brightness Off = new(0);

    public bool IsOff => Dp <= 0;

    /// <summary>Creates from a device value, clamping into 0..1000.</summary>
    public static Brightness FromDp(int dp) => new(Math.Clamp(dp, MinDp, MaxDp));

    /// <summary>Creates from a KNX percentage (0..100), scaling to the device range.</summary>
    public static Brightness FromPercent(int percent)
    {
        var clampedPercent = Math.Clamp(percent, 0, 100);
        var dp = (int)Math.Round(clampedPercent / 100.0 * MaxDp, MidpointRounding.AwayFromZero);
        return new Brightness(Math.Clamp(dp, MinDp, MaxDp));
    }

    /// <summary>Scales the device value back to a KNX percentage (0..100).</summary>
    public int ToPercent() => (int)Math.Round(Dp / (double)MaxDp * 100, MidpointRounding.AwayFromZero);

    public override string ToString() => $"{Dp} ({ToPercent()}%)";
}
