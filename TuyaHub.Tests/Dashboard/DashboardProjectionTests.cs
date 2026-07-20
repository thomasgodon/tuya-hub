using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Dashboard;
using TuyaHub.Infrastructure.Profiles;
using Xunit;

namespace TuyaHub.Tests.Dashboard;

/// <summary>
/// The dashboard device projection. Guards that Wind Calm still projects its bespoke fan+light DTO
/// unchanged (and carries no generic sections), while a capability-driven profile projects labelled
/// sections from the snapshot's capability values.
/// </summary>
public class DashboardProjectionTests
{
    [Fact]
    public void Wind_calm_projects_the_fan_and_light_dto_unchanged()
    {
        var state = new DeviceStateSnapshot(
            DeviceName.Create("LivingRoomFan"), IsOnline: true,
            FanPower: true, FanSpeedStatus: 3, FanDirection.Reverse, FanTimerMinutes: 30, FanTimerRunning: true,
            FanBeep: true, LightPower: true, LightCctDp: 1000, LightCctPercent: 100);

        var dto = DashboardSnapshotPublisher.Project(WindCalmProfile.Create(), state);

        Assert.Equal("wind-calm", dto.ProfileId);
        Assert.True(dto.Fan.Power);
        Assert.Equal(3, dto.Fan.SpeedStatus);
        Assert.Equal("Reverse", dto.Fan.Direction);
        Assert.Equal(30, dto.Fan.TimerMinutes);
        Assert.True(dto.Fan.Beep);
        Assert.True(dto.Light.Power);
        Assert.Equal(100, dto.Light.CctPercent);
        Assert.Empty(dto.Sections);
    }

    [Fact]
    public void A_capability_profile_projects_labelled_sections()
    {
        var power = new CapabilityKey("Power");
        var energy = new CapabilityKey("EnergyKwh");
        var profile = new DeviceProfile
        {
            ProfileId = "smart-plug",
            CreateAggregate = _ => throw new NotSupportedException(),
            Capabilities =
            [
                new CapabilityBinding { Key = power, Dashboard = new DashboardField("Power") },
                new CapabilityBinding { Key = energy, Dashboard = new DashboardField("Energy", "kWh") },
            ],
        };

        var state = new DeviceStateSnapshot(
            DeviceName.Create("Plug"), IsOnline: true,
            false, 0, FanDirection.Forward, 0, false, false, false, 0, 0)
        {
            Capabilities = new Dictionary<CapabilityKey, CapabilityValue>
            {
                [power] = CapabilityValue.Bool(true),
                [energy] = CapabilityValue.Int(42),
            },
        };

        var dto = DashboardSnapshotPublisher.Project(profile, state);

        Assert.Equal("smart-plug", dto.ProfileId);
        Assert.Collection(dto.Sections,
            s => { Assert.Equal("Power", s.Label); Assert.Equal("On", s.Value); },
            s => { Assert.Equal("Energy", s.Label); Assert.Equal("42 kWh", s.Value); });
    }
}
