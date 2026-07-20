namespace TuyaHub.Domain.ValueObjects;

/// <summary>
/// Light colour temperature (DP 23 <c>temp_value</c>). The firmware exposes only three discrete
/// steps — 0 (cool/white), 500 (warm-white) and 1000 (warm). Any input is snapped to the nearest
/// step. Writing this DP can flicker the light, so callers should only write it on an actual step
/// change.
/// </summary>
public readonly record struct ColourTemperature
{
    public static readonly int[] Steps = [0, 500, 1000];

    /// <summary>Colour temperature on the device scale (one of 0 / 500 / 1000).</summary>
    public int Dp { get; }

    private ColourTemperature(int dp) => Dp = dp;

    /// <summary>Creates from a device value, snapping to the nearest supported step.</summary>
    public static ColourTemperature FromDp(int dp) => new(Snap(dp));

    /// <summary>Creates from a KNX percentage (0..100), mapping to the nearest supported step.</summary>
    public static ColourTemperature FromPercent(int percent)
    {
        var clampedPercent = Math.Clamp(percent, 0, 100);
        var dp = (int)Math.Round(clampedPercent / 100.0 * 1000, MidpointRounding.AwayFromZero);
        return new ColourTemperature(Snap(dp));
    }

    /// <summary>Scales the device value back to a KNX percentage (0..100).</summary>
    public int ToPercent() => (int)Math.Round(Dp / 1000.0 * 100, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Moves to the adjacent supported step, wrapping around at the rails (a KNX long-press cycle,
    /// UC-07). Step up from the warmest step (1000) wraps to the coolest (0); step down from 0 wraps
    /// to 1000. Navigates by index into <see cref="Steps"/> — the steps are non-contiguous, so this
    /// is not <c>Dp ± 1</c>.
    /// </summary>
    public ColourTemperature Cycle(bool up)
    {
        var index = Array.IndexOf(Steps, Dp);
        if (index < 0)
        {
            index = 0; // Current value isn't a canonical step (shouldn't happen): start from the coolest.
        }

        var next = (index + (up ? 1 : -1) + Steps.Length) % Steps.Length;
        return new ColourTemperature(Steps[next]);
    }

    private static int Snap(int dp)
    {
        var nearest = Steps[0];
        var smallestDistance = Math.Abs(dp - Steps[0]);

        foreach (var step in Steps)
        {
            var distance = Math.Abs(dp - step);
            if (distance < smallestDistance)
            {
                smallestDistance = distance;
                nearest = step;
            }
        }

        return nearest;
    }

    public override string ToString() => $"{Dp} ({ToPercent()}%)";
}
