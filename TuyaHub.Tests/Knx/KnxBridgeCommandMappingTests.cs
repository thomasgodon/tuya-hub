using Knx.Falcon;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Knx;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Profiles;
using Xunit;

namespace TuyaHub.Tests.Knx;

public class KnxBridgeCommandMappingTests
{
    private static readonly DeviceName Fan = DeviceName.Create("Fan");
    private static readonly DeviceProfile WindCalm = WindCalmProfile.Create();

    private static Dictionary<GroupAddress, KnxCommandBinding> BuildBindings(DeviceMapping mapping)
        => KnxBridge.BuildCommandBindings(new DeviceMappingOptions { ["Fan"] = mapping }, _ => WindCalm);

    private static CapabilityKey CapabilityAt(GroupAddress address, DeviceMapping mapping)
        => BuildBindings(mapping)[address].Capability.Key;

    [Fact]
    public void Populated_command_gas_are_mapped_to_device_and_capability()
    {
        var mapping = new DeviceMapping
        {
            ["FanPowerCommand"] = "1/1/1",
            ["FanSpeedStep"] = "1/1/3",
            ["FanDirectionCommand"] = "1/1/5",
            ["FanTimerCommand"] = "1/1/7",
            ["LightPowerCommand"] = "1/1/9",
            ["LightCctCommand"] = "1/1/13",
        };
        var bindings = BuildBindings(mapping);

        Assert.Equal(Fan, bindings[GroupAddress.Parse("1/1/1")].Device);
        Assert.Equal(WindCalmCapabilities.FanPower, bindings[GroupAddress.Parse("1/1/1")].Capability.Key);
        Assert.Equal(WindCalmCapabilities.FanSpeed, bindings[GroupAddress.Parse("1/1/3")].Capability.Key);
        Assert.Equal(WindCalmCapabilities.FanDirection, bindings[GroupAddress.Parse("1/1/5")].Capability.Key);
        Assert.Equal(WindCalmCapabilities.FanTimer, bindings[GroupAddress.Parse("1/1/7")].Capability.Key);
        Assert.Equal(WindCalmCapabilities.LightPower, bindings[GroupAddress.Parse("1/1/9")].Capability.Key);
        Assert.Equal(WindCalmCapabilities.LightCct, bindings[GroupAddress.Parse("1/1/13")].Capability.Key);
    }

    [Fact]
    public void Empty_or_absent_command_ga_disables_that_capability()
    {
        var bindings = BuildBindings(new DeviceMapping
        {
            ["FanPowerCommand"] = "1/1/1",
            ["FanSpeedStep"] = "",           // disabled
            ["FanDirectionCommand"] = "   ", // whitespace also disables
        });

        Assert.True(bindings.ContainsKey(GroupAddress.Parse("1/1/1")));
        Assert.Single(bindings);
    }

    [Fact]
    public void Cct_command_is_mapped()
    {
        var mapping = new DeviceMapping { ["LightCctCommand"] = "1/1/13" };

        Assert.Equal(WindCalmCapabilities.LightCct, CapabilityAt(GroupAddress.Parse("1/1/13"), mapping));
    }

    [Fact]
    public void Cct_step_command_is_mapped()
    {
        var mapping = new DeviceMapping { ["LightCctStep"] = "1/1/16" };

        Assert.Equal(WindCalmCapabilities.LightCctStep, CapabilityAt(GroupAddress.Parse("1/1/16"), mapping));
    }

    [Fact]
    public void Status_gas_are_not_treated_as_command_gas()
    {
        var bindings = BuildBindings(new DeviceMapping
        {
            ["FanPowerStatus"] = "1/1/2",
            ["FanSpeedStatus"] = "1/1/4",
        });

        Assert.Empty(bindings);
    }
}
