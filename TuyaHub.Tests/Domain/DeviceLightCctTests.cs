using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using Xunit;

namespace TuyaHub.Tests.Domain;

public class DeviceLightCctTests
{
    private static Device NewDevice() => new(DeviceName.Create("Fan"));

    [Fact]
    public void SetColourTemperature_before_any_report_produces_a_command()
    {
        var device = NewDevice();

        var command = device.SetLightColourTemperature(ColourTemperature.FromDp(500));

        Assert.False(command.IsEmpty);
        Assert.Equal(ColourTemperature.FromDp(500), command.LightCct);
    }

    [Fact]
    public void SetColourTemperature_to_the_same_reported_step_is_empty_flicker_mitigation()
    {
        var device = NewDevice();
        device.ApplyReportedState(new DeviceReport { LightCct = ColourTemperature.FromDp(500) });

        var command = device.SetLightColourTemperature(ColourTemperature.FromDp(500));

        Assert.True(command.IsEmpty);
    }

    [Fact]
    public void SetColourTemperature_to_a_different_step_produces_a_command()
    {
        var device = NewDevice();
        device.ApplyReportedState(new DeviceReport { LightCct = ColourTemperature.FromDp(500) });

        var command = device.SetLightColourTemperature(ColourTemperature.FromDp(1000));

        Assert.False(command.IsEmpty);
        Assert.Equal(ColourTemperature.FromDp(1000), command.LightCct);
    }
}
