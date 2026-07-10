namespace TuyaHub.Domain.ValueObjects;

/// <summary>
/// Fan auto-off countdown (DP 64 <c>countdown_left_fan</c>) in minutes, 0..540 (up to 9 h).
/// The device MCU owns the countdown — the bridge only sets and reads the remaining minutes and
/// never counts down locally. Named <c>CountdownTimer</c> to avoid clashing with
/// <see cref="System.Threading.Timer"/>.
/// </summary>
public readonly record struct CountdownTimer
{
    public const int MinMinutes = 0;
    public const int MaxMinutes = 540;

    public int Minutes { get; }

    private CountdownTimer(int minutes) => Minutes = minutes;

    public static readonly CountdownTimer None = new(0);

    public bool IsRunning => Minutes > 0;

    /// <summary>Creates from a minutes value, clamping into 0..540.</summary>
    public static CountdownTimer FromMinutes(int minutes) => new(Math.Clamp(minutes, MinMinutes, MaxMinutes));

    public override string ToString() => $"{Minutes} min";
}
