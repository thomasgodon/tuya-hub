using TuyaHub.Domain;
using TuyaHub.Domain.Events;
using TuyaHub.Domain.ValueObjects;
using Xunit;

namespace TuyaHub.Tests.Domain;

/// <summary>
/// The collapsed feedback event: <see cref="Device.ApplyReportedState"/> emits one
/// <see cref="DeviceCapabilityChanged"/> per changed capability, carrying the KNX-facing scalar the
/// former typed events carried (speed status 0..6, direction 0/1, CCT %, minutes).
/// </summary>
public class DeviceCapabilityEventTests
{
    private static Device NewDevice() => new(DeviceName.Create("Fan"));

    private static Dictionary<CapabilityKey, CapabilityValue> Changes(IEnumerable<MediatR.INotification> events)
        => events.OfType<DeviceCapabilityChanged>().ToDictionary(e => e.Capability, e => e.Value);

    [Fact]
    public void First_report_emits_a_change_for_each_reported_capability()
    {
        var device = NewDevice();

        var changes = Changes(device.ApplyReportedState(new DeviceReport
        {
            FanPower = true,
            FanSpeed = SpeedLevel.Create(3),
            FanDirection = FanDirection.Reverse,
            FanTimer = CountdownTimer.FromMinutes(60),
            LightPower = true,
            LightCct = ColourTemperature.FromDp(1000),
        }));

        Assert.True(changes[WindCalmCapabilities.FanPower].AsBool());
        Assert.Equal(3, changes[WindCalmCapabilities.FanSpeed].AsInt());
        Assert.Equal(1, changes[WindCalmCapabilities.FanDirection].AsInt());
        Assert.Equal(60, changes[WindCalmCapabilities.FanTimer].AsInt());
        Assert.True(changes[WindCalmCapabilities.LightPower].AsBool());
        Assert.Equal(100, changes[WindCalmCapabilities.LightCct].AsInt());        // 1000/1000 → 100%
    }

    [Fact]
    public void Fan_speed_status_is_zero_when_the_fan_is_off()
    {
        var device = NewDevice();

        var changes = Changes(device.ApplyReportedState(new DeviceReport
        {
            FanPower = false,
            FanSpeed = SpeedLevel.Create(4),
        }));

        Assert.False(changes[WindCalmCapabilities.FanPower].AsBool());
        Assert.Equal(0, changes[WindCalmCapabilities.FanSpeed].AsInt());
    }

    [Fact]
    public void An_unchanged_second_report_emits_nothing()
    {
        var device = NewDevice();
        var report = new DeviceReport { LightPower = true, LightCct = ColourTemperature.FromDp(500) };
        device.ApplyReportedState(report);

        var changes = Changes(device.ApplyReportedState(report));

        Assert.Empty(changes);
    }

    [Fact]
    public void Only_the_changed_capability_is_re_emitted()
    {
        var device = NewDevice();
        device.ApplyReportedState(new DeviceReport { LightPower = true, LightCct = ColourTemperature.FromDp(0) });

        var changes = Changes(device.ApplyReportedState(new DeviceReport { LightCct = ColourTemperature.FromDp(1000) }));

        Assert.Single(changes);
        Assert.Equal(100, changes[WindCalmCapabilities.LightCct].AsInt());
    }
}
