using Knx.Falcon;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Knx;
using TuyaHub.Infrastructure.Options;
using Xunit;

namespace TuyaHub.Tests.Knx;

public class KnxBridgeCommandMappingTests
{
    private static readonly DeviceName Fan = DeviceName.Create("Fan");

    [Fact]
    public void Populated_command_gas_are_mapped_to_device_and_capability()
    {
        var bindings = KnxBridge.BuildCommandBindings(new DeviceMappingOptions
        {
            ["Fan"] = new DeviceMapping
            {
                FanPowerCommand = "1/1/1",
                FanSpeedStep = "1/1/3",
                FanDirectionCommand = "1/1/5",
                FanTimerCommand = "1/1/7",
                LightPowerCommand = "1/1/9",
                LightBrightnessCommand = "1/1/11",
            },
        });

        Assert.Equal(new KnxCommandBinding(Fan, CommandCapability.FanPower), bindings[GroupAddress.Parse("1/1/1")]);
        Assert.Equal(new KnxCommandBinding(Fan, CommandCapability.FanSpeedStep), bindings[GroupAddress.Parse("1/1/3")]);
        Assert.Equal(new KnxCommandBinding(Fan, CommandCapability.FanDirection), bindings[GroupAddress.Parse("1/1/5")]);
        Assert.Equal(new KnxCommandBinding(Fan, CommandCapability.FanTimer), bindings[GroupAddress.Parse("1/1/7")]);
        Assert.Equal(new KnxCommandBinding(Fan, CommandCapability.LightPower), bindings[GroupAddress.Parse("1/1/9")]);
        Assert.Equal(new KnxCommandBinding(Fan, CommandCapability.LightBrightness), bindings[GroupAddress.Parse("1/1/11")]);
    }

    [Fact]
    public void Empty_command_ga_disables_that_capability()
    {
        var bindings = KnxBridge.BuildCommandBindings(new DeviceMappingOptions
        {
            ["Fan"] = new DeviceMapping
            {
                FanPowerCommand = "1/1/1",
                FanSpeedStep = "",          // disabled
                FanDirectionCommand = "   ", // whitespace also disables
            },
        });

        Assert.True(bindings.ContainsKey(GroupAddress.Parse("1/1/1")));
        Assert.Single(bindings);
    }

    [Fact]
    public void Cct_command_is_not_mapped_deferred_to_m6()
    {
        var bindings = KnxBridge.BuildCommandBindings(new DeviceMappingOptions
        {
            ["Fan"] = new DeviceMapping
            {
                LightCctCommand = "1/1/13", // configured, but must be ignored until M6
            },
        });

        Assert.Empty(bindings);
        Assert.False(bindings.ContainsKey(GroupAddress.Parse("1/1/13")));
    }

    [Fact]
    public void Status_gas_are_not_treated_as_command_gas()
    {
        var bindings = KnxBridge.BuildCommandBindings(new DeviceMappingOptions
        {
            ["Fan"] = new DeviceMapping
            {
                FanPowerStatus = "1/1/2",
                FanSpeedStatus = "1/1/4",
            },
        });

        Assert.Empty(bindings);
    }
}
