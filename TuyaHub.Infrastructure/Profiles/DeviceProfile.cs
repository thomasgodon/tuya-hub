using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Infrastructure.Profiles;

/// <summary>
/// A supported device type: its stable <see cref="ProfileId"/> (referenced from
/// <c>TuyaOptions.Devices[].Profile</c>), a factory for its domain aggregate, and its capability table.
/// The Wind Calm fan+light is the first profile (<see cref="WindCalmProfile"/>); adding a new device
/// type is adding another <see cref="DeviceProfile"/> and registering it — the ACLs stay untouched.
/// </summary>
internal sealed record DeviceProfile
{
    public required string ProfileId { get; init; }

    /// <summary>Builds this type's aggregate for a configured device.</summary>
    public required Func<DeviceName, IDevice> CreateAggregate { get; init; }

    /// <summary>The capabilities this device type exposes (Tuya dps ↔ domain ↔ KNX group objects).</summary>
    public required IReadOnlyList<CapabilityBinding> Capabilities { get; init; }

    /// <summary>
    /// Raw datapoints written once on every (re)connect to force a device baseline the domain model does
    /// not otherwise control — e.g. Wind Calm silences its confirmation buzzer (DP 66) so no LAN command
    /// beeps. Keyed by DP-id string → wire value (the same shape <c>TuyaProfileCodec.ToDps</c> produces).
    /// Empty by default, so other profiles write nothing.
    /// </summary>
    public IReadOnlyDictionary<string, object> OnConnectDps { get; init; } = new Dictionary<string, object>();
}
