using Knx.Falcon;
using TuyaHub.Application.Commands;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Dashboard;
using TuyaHub.Infrastructure.Knx;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Profiles;
using TuyaHub.Infrastructure.Tuya;
using Xunit;

namespace TuyaHub.Tests.Profiles;

/// <summary>
/// The acceptance test for the whole genericity effort: a device type the shared ACL code has never
/// seen — a smart plug (Power + EnergyKwh) — wires end-to-end through the Tuya codec, the KNX
/// store/command builders and translator, and the dashboard projection, with ZERO edits to any of those
/// shared files. Adding a device type is adding a profile, nothing else.
/// </summary>
public class SecondProfileGenericityTests
{
    private static readonly CapabilityKey Power = new("Power");
    private static readonly CapabilityKey Energy = new("EnergyKwh");
    private static readonly DeviceName Plug = DeviceName.Create("Plug");

    // A throwaway command record for the plug's power (proves the translator delegates to the binding).
    private sealed record SetPlugPowerCommand(DeviceName Device, bool On) : IDeviceCommand;

    private static DeviceProfile SmartPlug() => new()
    {
        ProfileId = "smart-plug",
        CreateAggregate = _ => throw new NotSupportedException("not needed for this test"),
        Capabilities =
        [
            new CapabilityBinding
            {
                Key = Power,
                Dp = 1,
                EncodeDp = v => (bool)v,
                DecodeDp = raw => Convert.ToBoolean(raw),
                StatusMappingKey = "PowerStatus",
                EncodeStatus = v => KnxDpt.Bool(v.AsBool()),
                CommandMappingKey = "PowerCommand",
                BuildCommand = (device, payload) => new SetPlugPowerCommand(device, KnxDpt.DecodeBool(payload)),
                Dashboard = new DashboardField("Power"),
            },
            new CapabilityBinding
            {
                Key = Energy,
                Dp = 17,
                EncodeDp = v => (int)v,
                DecodeDp = raw => Convert.ToInt32(raw),
                StatusMappingKey = "EnergyStatus",
                EncodeStatus = v => KnxDpt.Count(v.AsInt()),
                Dashboard = new DashboardField("Energy", "kWh"),
            },
        ],
    };

    [Fact]
    public void Tuya_codec_handles_the_new_profile()
    {
        var profile = SmartPlug();

        var dps = TuyaProfileCodec.ToDps(profile, DeviceCommand.Empty.With(Power, true));
        Assert.Equal(true, dps["1"]);

        var report = TuyaProfileCodec.ToReport(profile, new Dictionary<int, object> { [1] = true, [17] = 42 });
        Assert.Equal(true, (bool)report.Values[Power]);
        Assert.Equal(42, (int)report.Values[Energy]);
    }

    [Fact]
    public void Knx_store_and_command_bindings_handle_the_new_profile()
    {
        var mappings = new DeviceMappingOptions
        {
            ["Plug"] = new DeviceMapping
            {
                ["PowerCommand"] = "2/1/1",
                ["PowerStatus"] = "2/1/2",
                ["EnergyStatus"] = "2/1/3",
            },
        };

        var store = KnxBridge.BuildStore(mappings, _ => SmartPlug());
        Assert.Equal(GroupAddress.Parse("2/1/2"), store[(Plug, Power)].Address);
        Assert.Equal(GroupAddress.Parse("2/1/3"), store[(Plug, Energy)].Address);

        var bindings = KnxBridge.BuildCommandBindings(mappings, _ => SmartPlug());
        Assert.Single(bindings); // only Power has a command
        var command = KnxCommandTranslator.Translate(bindings[GroupAddress.Parse("2/1/1")], [0x01]);
        var plugPower = Assert.IsType<SetPlugPowerCommand>(command);
        Assert.True(plugPower.On);
    }

    [Fact]
    public void Dashboard_projects_sections_for_the_new_profile()
    {
        var state = new DeviceStateSnapshot(
            Plug, IsOnline: true, false, 0, FanDirection.Forward, 0, false, false, false, 0, 0)
        {
            Capabilities = new Dictionary<CapabilityKey, CapabilityValue>
            {
                [Power] = CapabilityValue.Bool(true),
                [Energy] = CapabilityValue.Int(42),
            },
        };

        var dto = DashboardSnapshotPublisher.Project(SmartPlug(), state);

        Assert.Equal("smart-plug", dto.ProfileId);
        Assert.Collection(dto.Sections,
            s => { Assert.Equal("Power", s.Label); Assert.Equal("On", s.Value); },
            s => { Assert.Equal("Energy", s.Label); Assert.Equal("42 kWh", s.Value); });
    }
}
