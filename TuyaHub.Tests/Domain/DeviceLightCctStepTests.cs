using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using Xunit;

namespace TuyaHub.Tests.Domain;

public class DeviceLightCctStepTests
{
    private static Device NewDevice() => new(DeviceName.Create("Fan"));

    private static Device WithReportedCct(int dp)
    {
        var device = NewDevice();
        device.ApplyReportedState(new DeviceReport { LightCct = ColourTemperature.FromDp(dp) });
        return device;
    }

    [Theory]
    [InlineData(0, 500)]
    [InlineData(500, 1000)]
    [InlineData(1000, 0)]   // wrap
    public void StepUp_cycles_to_next_step_wrapping_at_the_rail(int fromDp, int expectedDp)
    {
        var device = WithReportedCct(fromDp);

        var command = device.CycleLightColourTemperature(up: true);

        Assert.False(command.IsEmpty);
        Assert.Equal(ColourTemperature.FromDp(expectedDp), command.LightCct);
    }

    [Theory]
    [InlineData(1000, 500)]
    [InlineData(500, 0)]
    [InlineData(0, 1000)]   // wrap
    public void StepDown_cycles_to_previous_step_wrapping_at_the_rail(int fromDp, int expectedDp)
    {
        var device = WithReportedCct(fromDp);

        var command = device.CycleLightColourTemperature(up: false);

        Assert.False(command.IsEmpty);
        Assert.Equal(ColourTemperature.FromDp(expectedDp), command.LightCct);
    }

    [Fact]
    public void Repeated_step_up_loops_full_cycle()
    {
        var device = WithReportedCct(0);
        var sequence = new[] { 500, 1000, 0, 500 };

        foreach (var expectedDp in sequence)
        {
            var command = device.CycleLightColourTemperature(up: true);
            Assert.Equal(ColourTemperature.FromDp(expectedDp), command.LightCct);
            device.ApplyReportedState(new DeviceReport { LightCct = command.LightCct });
        }
    }

    [Fact]
    public void Step_before_any_report_starts_from_the_coolest_step()
    {
        var device = NewDevice(); // default CCT is Dp 0

        var command = device.CycleLightColourTemperature(up: true);

        Assert.False(command.IsEmpty);
        Assert.Equal(ColourTemperature.FromDp(500), command.LightCct);
    }
}
