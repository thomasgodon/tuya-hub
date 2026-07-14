using Newtonsoft.Json;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Profiles;
using TuyaHub.Infrastructure.Tuya;
using Xunit;

namespace TuyaHub.Tests.Tuya;

/// <summary>
/// The profile-driven Tuya codec, exercised with the Wind Calm profile. Guards that outbound dps and
/// inbound reports translate exactly as the old wind-calm-only <c>TuyaDatapoints</c> did — including the
/// #1 quirk: fan speed (DP 62) must serialize as a JSON number, never a string.
/// </summary>
public class TuyaProfileCodecTests
{
    private static readonly DeviceProfile Profile = WindCalmProfile.Create();

    [Fact]
    public void ToDps_encodes_every_capability_with_its_dp()
    {
        var command = new DeviceCommand
        {
            FanPower = true,
            FanSpeed = SpeedLevel.Create(3),
            FanDirection = FanDirection.Reverse,
            FanTimer = CountdownTimer.FromMinutes(30),
            LightPower = false,
            LightBrightness = Brightness.FromDp(500),
            LightCct = ColourTemperature.FromDp(1000),
        };

        var dps = TuyaProfileCodec.ToDps(Profile, command);

        Assert.Equal(true, dps["60"]);
        Assert.Equal(3, dps["62"]);
        Assert.Equal("reverse", dps["63"]);
        Assert.Equal(30, dps["64"]);
        Assert.Equal(false, dps["20"]);
        Assert.Equal(500, dps["22"]);
        Assert.Equal(1000, dps["23"]);
    }

    [Fact]
    public void ToDps_fan_speed_serializes_as_a_json_number_not_a_string()
    {
        var dps = TuyaProfileCodec.ToDps(Profile, new DeviceCommand { FanSpeed = SpeedLevel.Create(5) });

        var json = JsonConvert.SerializeObject(new Dictionary<string, object> { ["dps"] = dps });

        Assert.Contains("\"62\":5", json);
        Assert.DoesNotContain("\"62\":\"5\"", json);
    }

    [Fact]
    public void ToDps_includes_only_set_capabilities()
    {
        var dps = TuyaProfileCodec.ToDps(Profile, new DeviceCommand { FanPower = true });

        Assert.Single(dps);
        Assert.True(dps.ContainsKey("60"));
    }

    [Fact]
    public void ToReport_round_trips_all_capabilities()
    {
        var dps = new Dictionary<int, object>
        {
            [60] = true,
            [62] = 4,
            [63] = "reverse",
            [64] = 30,
            [20] = true,
            [22] = 500,
            [23] = 1000,
        };

        var report = TuyaProfileCodec.ToReport(Profile, dps);

        Assert.Equal(true, report.FanPower);
        Assert.Equal(SpeedLevel.Create(4), report.FanSpeed);
        Assert.Equal(FanDirection.Reverse, report.FanDirection);
        Assert.Equal(CountdownTimer.FromMinutes(30), report.FanTimer);
        Assert.Equal(true, report.LightPower);
        Assert.Equal(Brightness.FromDp(500), report.LightBrightness);
        Assert.Equal(ColourTemperature.FromDp(1000), report.LightCct);
    }

    [Fact]
    public void ToReport_drops_fan_speed_below_one()
    {
        var report = TuyaProfileCodec.ToReport(Profile, new Dictionary<int, object> { [62] = 0 });

        Assert.Null(report.FanSpeed);
        Assert.Empty(report.Values);
    }

    [Fact]
    public void ToReport_ignores_unmapped_datapoints()
    {
        var report = TuyaProfileCodec.ToReport(Profile, new Dictionary<int, object> { [99] = 1 });

        Assert.Empty(report.Values);
    }
}
