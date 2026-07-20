using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Profiles;
using Xunit;

namespace TuyaHub.Tests.Profiles;

/// <summary>
/// The device-profile registry and the Wind Calm profile (#1). Guards resolution by id and that Wind
/// Calm still declares exactly its historical capability set and DP numbers.
/// </summary>
public class DeviceProfileRegistryTests
{
    private static IDeviceProfileRegistry Registry() => new DeviceProfileRegistry([WindCalmProfile.Create()]);

    [Fact]
    public void Get_resolves_the_wind_calm_profile()
    {
        var profile = Registry().Get(WindCalmProfile.ProfileId);

        Assert.Equal("wind-calm", profile.ProfileId);
        Assert.IsType<Device>(profile.CreateAggregate(DeviceName.Create("Fan")));
    }

    [Fact]
    public void Get_is_case_insensitive()
    {
        Assert.Equal("wind-calm", Registry().Get("Wind-Calm").ProfileId);
    }

    [Fact]
    public void Get_throws_for_an_unknown_profile()
    {
        Assert.Throws<InvalidOperationException>(() => Registry().Get("thermostat"));
    }

    [Fact]
    public void Wind_calm_declares_its_datapoints()
    {
        var profile = WindCalmProfile.Create();
        var dpByKey = profile.Capabilities.ToDictionary(c => c.Key, c => c.Dp);

        Assert.Equal(60, dpByKey[WindCalmCapabilities.FanPower]);
        Assert.Equal(62, dpByKey[WindCalmCapabilities.FanSpeed]);
        Assert.Equal(63, dpByKey[WindCalmCapabilities.FanDirection]);
        Assert.Equal(64, dpByKey[WindCalmCapabilities.FanTimer]);
        Assert.Equal(66, dpByKey[WindCalmCapabilities.FanBeep]);
        Assert.Equal(20, dpByKey[WindCalmCapabilities.LightPower]);
        Assert.Equal(23, dpByKey[WindCalmCapabilities.LightCct]);
        Assert.Null(dpByKey[WellKnownCapabilities.Availability]);
    }

    [Fact]
    public void Wind_calm_availability_is_status_only()
    {
        var availability = WindCalmProfile.Create().Capabilities
            .Single(c => c.Key == WellKnownCapabilities.Availability);

        Assert.Equal("AvailabilityStatus", availability.StatusMappingKey);
        Assert.Null(availability.CommandMappingKey);
        Assert.Null(availability.BuildCommand);
    }
}
