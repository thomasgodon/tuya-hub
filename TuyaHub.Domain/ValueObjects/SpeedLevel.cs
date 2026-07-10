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

    public override string ToString() => Value.ToString();
}
