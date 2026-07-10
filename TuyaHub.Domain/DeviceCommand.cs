using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain;

/// <summary>
/// A set of intended datapoint writes expressed in domain terms. Produced by the <see cref="Device"/>
/// aggregate's command methods and translated into a Tuya <c>dps</c> dictionary by the Tuya ACL —
/// the aggregate never speaks DP numbers. Only the properties that are set are written; an
/// <see cref="IsEmpty"/> command means "nothing to send" (e.g. a no-op dim step or an unchanged CCT).
/// </summary>
public sealed record DeviceCommand
{
    public bool? FanPower { get; init; }
    public SpeedLevel? FanSpeed { get; init; }
    public FanDirection? FanDirection { get; init; }
    public CountdownTimer? FanTimer { get; init; }
    public bool? LightPower { get; init; }
    public Brightness? LightBrightness { get; init; }
    public ColourTemperature? LightCct { get; init; }

    public static readonly DeviceCommand Empty = new();

    public bool IsEmpty =>
        FanPower is null && FanSpeed is null && FanDirection is null && FanTimer is null &&
        LightPower is null && LightBrightness is null && LightCct is null;
}
