using Knx.Falcon;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Knx;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Profiles;
using Xunit;

namespace TuyaHub.Tests.Knx;

/// <summary>
/// Golden characterization of the Wind Calm KNX wiring: mirrors the shipped
/// <c>TuyaHub/appsettings.json</c> <c>DeviceMappings</c> (1/1/1 … 1/1/16) and asserts the profile-driven
/// store and command bindings resolve exactly the historical group-address layout. Guards the refactor
/// against any drift in the wind-calm status/command mapping.
/// </summary>
public class WindCalmWiringGoldenTests
{
    private static readonly DeviceName Fan = DeviceName.Create("LivingRoomFan");

    // Mirrors TuyaHub/appsettings.json → DeviceMappings.LivingRoomFan.
    private static DeviceMappingOptions ShippedMappings() => new()
    {
        ["LivingRoomFan"] = new DeviceMapping
        {
            ["FanPowerCommand"] = "1/1/1",
            ["FanPowerStatus"] = "1/1/2",
            ["FanSpeedStep"] = "1/1/3",
            ["FanSpeedStatus"] = "1/1/4",
            ["FanDirectionCommand"] = "1/1/5",
            ["FanDirectionStatus"] = "1/1/6",
            ["FanTimerCommand"] = "1/1/7",
            ["FanTimerStatus"] = "1/1/8",
            ["FanBeepCommand"] = "1/1/11",
            ["FanBeepStatus"] = "1/1/12",
            ["LightPowerCommand"] = "1/1/9",
            ["LightPowerStatus"] = "1/1/10",
            ["LightCctCommand"] = "1/1/13",
            ["LightCctStatus"] = "1/1/14",
            ["LightCctStep"] = "1/1/16",
            ["AvailabilityStatus"] = "1/1/15",
        },
    };

    [Fact]
    public void Status_store_matches_the_shipped_layout()
    {
        var store = KnxBridge.BuildStore(ShippedMappings(), _ => WindCalmProfile.Create());

        Assert.Equal(8, store.Count);
        Assert.Equal(GroupAddress.Parse("1/1/2"), store[(Fan, WindCalmCapabilities.FanPower)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/4"), store[(Fan, WindCalmCapabilities.FanSpeed)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/6"), store[(Fan, WindCalmCapabilities.FanDirection)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/8"), store[(Fan, WindCalmCapabilities.FanTimer)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/12"), store[(Fan, WindCalmCapabilities.FanBeep)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/10"), store[(Fan, WindCalmCapabilities.LightPower)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/14"), store[(Fan, WindCalmCapabilities.LightCct)].Address);
        Assert.Equal(GroupAddress.Parse("1/1/15"), store[(Fan, WellKnownCapabilities.Availability)].Address);
    }

    [Fact]
    public void Command_bindings_match_the_shipped_layout()
    {
        var bindings = KnxBridge.BuildCommandBindings(ShippedMappings(), _ => WindCalmProfile.Create());
        CapabilityKey KeyAt(string ga) => bindings[GroupAddress.Parse(ga)].Capability.Key;

        Assert.Equal(8, bindings.Count);
        Assert.Equal(WindCalmCapabilities.FanPower, KeyAt("1/1/1"));
        Assert.Equal(WindCalmCapabilities.FanSpeed, KeyAt("1/1/3"));
        Assert.Equal(WindCalmCapabilities.FanDirection, KeyAt("1/1/5"));
        Assert.Equal(WindCalmCapabilities.FanTimer, KeyAt("1/1/7"));
        Assert.Equal(WindCalmCapabilities.FanBeep, KeyAt("1/1/11"));
        Assert.Equal(WindCalmCapabilities.LightPower, KeyAt("1/1/9"));
        Assert.Equal(WindCalmCapabilities.LightCct, KeyAt("1/1/13"));
        Assert.Equal(WindCalmCapabilities.LightCctStep, KeyAt("1/1/16"));
    }
}
