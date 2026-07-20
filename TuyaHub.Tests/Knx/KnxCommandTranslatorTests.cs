using TuyaHub.Application.Commands;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Knx;
using TuyaHub.Infrastructure.Profiles;
using Xunit;

namespace TuyaHub.Tests.Knx;

public class KnxCommandTranslatorTests
{
    private static readonly DeviceName Fan = DeviceName.Create("Fan");
    private static readonly DeviceProfile WindCalm = WindCalmProfile.Create();

    private static IDeviceCommand? Translate(CapabilityKey capability, params byte[] payload)
    {
        var binding = WindCalm.Capabilities.Single(c => c.Key == capability);
        return KnxCommandTranslator.Translate(new KnxCommandBinding(Fan, binding), payload);
    }

    [Theory]
    [InlineData(0x01, true)]
    [InlineData(0x00, false)]
    public void FanPower_maps_to_set_fan_power(byte payload, bool expectedOn)
    {
        var command = Assert.IsType<SetFanPowerCommand>(Translate(WindCalmCapabilities.FanPower, payload));
        Assert.Equal(Fan, command.Device);
        Assert.Equal(expectedOn, command.On);
    }

    [Theory]
    [InlineData(0x01, true)]
    [InlineData(0x00, false)]
    public void LightPower_maps_to_set_light_power(byte payload, bool expectedOn)
    {
        var command = Assert.IsType<SetLightPowerCommand>(Translate(WindCalmCapabilities.LightPower, payload));
        Assert.Equal(expectedOn, command.On);
    }

    [Theory]
    [InlineData(0x09, true)]    // up
    [InlineData(0x01, false)]   // down
    public void FanSpeedStep_maps_to_step_fan_speed(byte payload, bool expectedUp)
    {
        var command = Assert.IsType<StepFanSpeedCommand>(Translate(WindCalmCapabilities.FanSpeed, payload));
        Assert.Equal(expectedUp, command.Up);
    }

    [Theory]
    [InlineData(0x00)]   // down break
    [InlineData(0x08)]   // up break
    public void FanSpeedStep_break_produces_no_command(byte payload)
    {
        Assert.Null(Translate(WindCalmCapabilities.FanSpeed, payload));
    }

    [Theory]
    [InlineData(0x00, FanDirection.Forward)]
    [InlineData(0x01, FanDirection.Reverse)]
    public void FanDirection_maps_bit_to_direction(byte payload, FanDirection expected)
    {
        var command = Assert.IsType<SetFanDirectionCommand>(Translate(WindCalmCapabilities.FanDirection, payload));
        Assert.Equal(expected, command.Direction);
    }

    [Fact]
    public void FanTimer_maps_big_endian_minutes()
    {
        var command = Assert.IsType<SetFanTimerCommand>(Translate(WindCalmCapabilities.FanTimer, 0x02, 0x1C));
        Assert.Equal(540, command.Minutes);
    }

    [Fact]
    public void LightCct_maps_scaled_percent()
    {
        var command = Assert.IsType<SetLightCctCommand>(Translate(WindCalmCapabilities.LightCct, 0xFF));
        Assert.Equal(100, command.Percent);
    }

    [Theory]
    [InlineData(0x09, true)]    // up
    [InlineData(0x01, false)]   // down
    public void LightCctStep_maps_to_step_light_cct(byte payload, bool expectedUp)
    {
        var command = Assert.IsType<StepLightCctCommand>(Translate(WindCalmCapabilities.LightCctStep, payload));
        Assert.Equal(Fan, command.Device);
        Assert.Equal(expectedUp, command.Up);
    }

    [Theory]
    [InlineData(0x00)]   // down break
    [InlineData(0x08)]   // up break
    public void LightCctStep_break_produces_no_command(byte payload)
    {
        Assert.Null(Translate(WindCalmCapabilities.LightCctStep, payload));
    }
}
