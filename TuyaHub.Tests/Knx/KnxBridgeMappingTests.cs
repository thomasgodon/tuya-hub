using Knx.Falcon;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Knx;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Profiles;
using Xunit;

namespace TuyaHub.Tests.Knx;

public class KnxBridgeMappingTests
{
    private static readonly DeviceName Fan = DeviceName.Create("Fan");
    private static readonly DeviceProfile WindCalm = WindCalmProfile.Create();

    private static Dictionary<(DeviceName, CapabilityKey), KnxStatusValue> BuildStore(DeviceMapping mapping)
        => KnxBridge.BuildStore(new DeviceMappingOptions { ["Fan"] = mapping }, _ => WindCalm);

    [Fact]
    public void Populated_status_gas_are_mapped_to_their_addresses()
    {
        var store = BuildStore(new DeviceMapping
        {
            ["FanPowerStatus"] = "1/1/2",
            ["FanSpeedStatus"] = "1/1/4",
            ["FanTimerStatus"] = "1/1/8",
            ["LightBrightnessStatus"] = "1/1/12",
            ["AvailabilityStatus"] = "1/1/15",
        });

        Assert.Equal(GroupAddress.Parse("1/1/2"), store[(Fan, WindCalmCapabilities.FanPower)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/4"), store[(Fan, WindCalmCapabilities.FanSpeed)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/8"), store[(Fan, WindCalmCapabilities.FanTimer)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/12"), store[(Fan, WindCalmCapabilities.LightBrightness)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/15"), store[(Fan, WellKnownCapabilities.Availability)].Address);
    }

    [Fact]
    public void Empty_or_absent_status_ga_disables_that_capability()
    {
        var store = BuildStore(new DeviceMapping
        {
            ["FanPowerStatus"] = "1/1/2",
            ["FanSpeedStatus"] = "",          // disabled
            ["FanDirectionStatus"] = "   ",   // whitespace also disables
            // FanTimerStatus absent — also disabled
        });

        Assert.True(store.ContainsKey((Fan, WindCalmCapabilities.FanPower)));
        Assert.False(store.ContainsKey((Fan, WindCalmCapabilities.FanSpeed)));
        Assert.False(store.ContainsKey((Fan, WindCalmCapabilities.FanDirection)));
        Assert.False(store.ContainsKey((Fan, WindCalmCapabilities.FanTimer)));
    }

    [Fact]
    public void Cct_status_is_mapped()
    {
        var store = BuildStore(new DeviceMapping { ["LightCctStatus"] = "1/1/14" });

        Assert.Equal(GroupAddress.Parse("1/1/14"), store[(Fan, WindCalmCapabilities.LightCct)].Address);
    }
}
