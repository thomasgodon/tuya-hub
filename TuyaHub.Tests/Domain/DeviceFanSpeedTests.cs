using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using Xunit;

namespace TuyaHub.Tests.Domain;

public class DeviceFanSpeedTests
{
    private static Device NewDevice() => new(DeviceName.Create("Fan"));

    private static Device DeviceRunningAt(int level)
    {
        var device = NewDevice();
        device.ApplyReportedState(new DeviceReport { FanPower = true, FanSpeed = SpeedLevel.Create(level) });
        return device;
    }

    [Fact]
    public void Zero_percent_while_off_sends_nothing()
    {
        var command = NewDevice().SetFanSpeedPercent(0);

        Assert.True(command.IsEmpty);
    }

    [Fact]
    public void Zero_percent_while_on_turns_the_fan_off()
    {
        var command = DeviceRunningAt(3).SetFanSpeedPercent(0);

        Assert.False(command.IsEmpty);
        Assert.Equal(false, command.FanPower);
    }

    [Fact]
    public void Setting_a_speed_while_off_turns_the_fan_on_at_that_level()
    {
        var command = NewDevice().SetFanSpeedPercent(50);

        Assert.Equal(true, command.FanPower);
        Assert.Equal(SpeedLevel.Create(3), command.FanSpeed);
    }

    [Fact]
    public void Setting_a_different_speed_while_on_sends_only_the_level()
    {
        var command = DeviceRunningAt(3).SetFanSpeedPercent(100);

        Assert.Null(command.FanPower);
        Assert.Equal(SpeedLevel.Create(6), command.FanSpeed);
    }

    [Fact]
    public void Setting_the_same_speed_while_on_sends_nothing()
    {
        var command = DeviceRunningAt(3).SetFanSpeedPercent(50); // 50 % → level 3, already there

        Assert.True(command.IsEmpty);
    }
}
