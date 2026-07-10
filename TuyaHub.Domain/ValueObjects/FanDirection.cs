namespace TuyaHub.Domain.ValueObjects;

/// <summary>
/// Blade direction of the fan (DP 63 <c>fan_direction</c>). <see cref="Forward"/> is summer mode,
/// <see cref="Reverse"/> is winter mode. KNX maps 0 = forward/summer, 1 = reverse/winter.
/// </summary>
public enum FanDirection
{
    Forward = 0,
    Reverse = 1,
}
