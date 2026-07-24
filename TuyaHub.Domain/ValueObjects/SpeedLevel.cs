namespace TuyaHub.Domain.ValueObjects;

/// <summary>
/// Fan speed level (DP 62 <c>fan_speed</c>), constrained to the device range 1..6.
/// Always serialized to the device as an integer — never a string.
/// </summary>
public readonly record struct SpeedLevel
{
    public const int Min = 1;
    public const int Max = 6;

    public int Value { get; }

    private SpeedLevel(int value) => Value = value;

    public static readonly SpeedLevel Lowest = new(Min);
    public static readonly SpeedLevel Highest = new(Max);

    /// <summary>Creates a level, clamping into the valid 1..6 range.</summary>
    public static SpeedLevel Clamp(int value) => new(Math.Clamp(value, Min, Max));

    /// <summary>Creates a level, throwing if outside 1..6 (use for validated device readback).</summary>
    public static SpeedLevel Create(int value)
    {
        if (value is < Min or > Max)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Speed level must be {Min}..{Max}.");
        }

        return new SpeedLevel(value);
    }

    public SpeedLevel Up() => Clamp(Value + 1);

    public SpeedLevel Down() => Clamp(Value - 1);

    /// <summary>
    /// Maps a raw status level (0 = off, 1..6) to a KNX DPT 5.001 percentage: 0 → 0 %, otherwise
    /// <c>round(level * 100 / Max)</c> (1→17, 2→33, 3→50, 4→67, 5→83, 6→100).
    /// </summary>
    public static int ToPercent(int level)
        => level <= 0 ? 0 : (int)Math.Round(level * 100.0 / Max, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Maps a KNX percentage (1..100) to a level 1..6 via <c>ceil(percent / 100 * Max)</c>
    /// (1–16%→1, 17–33%→2, 34–50%→3, 51–67%→4, 68–83%→5, 84–100%→6). The 0 % case ("fan off") is the
    /// caller's concern — fan power is a separate DP.
    /// </summary>
    public static SpeedLevel FromPercent(int percent)
        => Clamp((int)Math.Ceiling(Math.Clamp(percent, 1, 100) * Max / 100.0));

    public override string ToString() => Value.ToString();
}
