using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using Xunit;

namespace TuyaHub.Tests.Domain;

/// <summary>
/// The generic capability-bag backing of <see cref="DeviceCommand"/> / <see cref="DeviceReport"/> and
/// their Wind Calm typed facade. Guards that the facade is a transparent view over the bag and that the
/// generic <see cref="DeviceCommand.With"/> builder clones (never shares) the bag.
/// </summary>
public class DeviceCommandBagTests
{
    [Fact]
    public void Typed_facade_setter_populates_the_bag_under_the_wind_calm_key()
    {
        var command = new DeviceCommand { FanSpeed = SpeedLevel.Create(3) };

        Assert.True(command.Values.ContainsKey(WindCalmCapabilities.FanSpeed));
        Assert.Equal(SpeedLevel.Create(3), (SpeedLevel)command.Values[WindCalmCapabilities.FanSpeed]);
        Assert.Equal(SpeedLevel.Create(3), command.FanSpeed);
    }

    [Fact]
    public void Unset_facade_property_is_null_and_absent_from_the_bag()
    {
        var command = new DeviceCommand { FanPower = true };

        Assert.Null(command.FanSpeed);
        Assert.False(command.Values.ContainsKey(WindCalmCapabilities.FanSpeed));
        Assert.Single(command.Values);
    }

    [Fact]
    public void Setting_a_facade_property_to_null_removes_it_from_the_bag()
    {
        var command = new DeviceCommand { LightCct = null };

        Assert.Null(command.LightCct);
        Assert.True(command.IsEmpty);
    }

    [Fact]
    public void Empty_command_has_an_empty_bag()
    {
        Assert.True(DeviceCommand.Empty.IsEmpty);
        Assert.Empty(DeviceCommand.Empty.Values);
    }

    [Fact]
    public void Report_facade_round_trips_all_wind_calm_capabilities()
    {
        var report = new DeviceReport
        {
            FanPower = true,
            FanSpeed = SpeedLevel.Create(4),
            FanDirection = FanDirection.Reverse,
            FanTimer = CountdownTimer.FromMinutes(30),
            FanBeep = true,
            LightPower = true,
            LightCct = ColourTemperature.FromDp(1000),
        };

        Assert.Equal(true, report.FanPower);
        Assert.Equal(SpeedLevel.Create(4), report.FanSpeed);
        Assert.Equal(FanDirection.Reverse, report.FanDirection);
        Assert.Equal(CountdownTimer.FromMinutes(30), report.FanTimer);
        Assert.Equal(true, report.FanBeep);
        Assert.Equal(true, report.LightPower);
        Assert.Equal(ColourTemperature.FromDp(1000), report.LightCct);
        Assert.Equal(7, report.Values.Count);
    }

    [Fact]
    public void With_returns_a_copy_and_does_not_mutate_the_original()
    {
        var custom = new CapabilityKey("Power");
        var original = DeviceCommand.Empty;

        var updated = original.With(custom, true);

        Assert.True(original.IsEmpty);
        Assert.True(updated.Values.ContainsKey(custom));
        Assert.True((bool)updated.Values[custom]);
    }
}
