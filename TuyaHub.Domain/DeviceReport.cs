using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain;

/// <summary>
/// An observed snapshot of device state, translated from a Tuya <c>dps</c> update by the Tuya ACL
/// into domain terms. A <c>null</c> property means that datapoint was not present in this update
/// (partial updates are normal for pushed status). Fed to <see cref="Device.ApplyReportedState"/>,
/// which treats device readback as authoritative.
/// </summary>
public sealed record DeviceReport
{
    public bool? FanPower { get; init; }
    public SpeedLevel? FanSpeed { get; init; }
    public FanDirection? FanDirection { get; init; }
    public CountdownTimer? FanTimer { get; init; }
    public bool? LightPower { get; init; }
    public Brightness? LightBrightness { get; init; }
    public ColourTemperature? LightCct { get; init; }
}
