using Knx.Falcon;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Knx;
using TuyaHub.Infrastructure.Options;
using Xunit;

namespace TuyaHub.Tests.Knx;

public class KnxBridgeMappingTests
{
    private static readonly DeviceName Fan = DeviceName.Create("Fan");

    [Fact]
    public void Populated_status_gas_are_mapped_to_their_addresses()
    {
        var store = KnxBridge.BuildStore(new DeviceMappingOptions
        {
            ["Fan"] = new DeviceMapping
            {
                FanPowerStatus = "1/1/2",
                FanSpeedStatus = "1/1/4",
                FanTimerStatus = "1/1/8",
                LightBrightnessStatus = "1/1/12",
                AvailabilityStatus = "1/1/15",
            },
        });

        Assert.Equal(GroupAddress.Parse("1/1/2"), store[(Fan, Capability.FanPower)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/4"), store[(Fan, Capability.FanSpeed)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/8"), store[(Fan, Capability.FanTimer)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/12"), store[(Fan, Capability.LightBrightness)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/15"), store[(Fan, Capability.Availability)].Address);
    }

    [Fact]
    public void Empty_status_ga_disables_that_capability()
    {
        var store = KnxBridge.BuildStore(new DeviceMappingOptions
        {
            ["Fan"] = new DeviceMapping
            {
                FanPowerStatus = "1/1/2",
                FanSpeedStatus = "",          // disabled
                FanDirectionStatus = "   ",   // whitespace also disables
            },
        });

        Assert.True(store.ContainsKey((Fan, Capability.FanPower)));
        Assert.False(store.ContainsKey((Fan, Capability.FanSpeed)));
        Assert.False(store.ContainsKey((Fan, Capability.FanDirection)));
    }

    [Fact]
    public void Cct_status_is_mapped()
    {
        var store = KnxBridge.BuildStore(new DeviceMappingOptions
        {
            ["Fan"] = new DeviceMapping
            {
                LightCctStatus = "1/1/14",
            },
        });

        Assert.Equal(GroupAddress.Parse("1/1/14"), store[(Fan, Capability.LightCct)].Address);
    }
}
