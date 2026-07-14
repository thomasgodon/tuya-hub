using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Profiles;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// Binds one inbound command group address to the device and the profile capability binding it drives.
/// The command-GA lookup maps a <see cref="Knx.Falcon.GroupAddress"/> to this record so an incoming
/// <c>GroupValueWrite</c> can be routed and decoded via the binding's <c>BuildCommand</c> (the inbound
/// counterpart of <see cref="KnxStatusValue"/>).
/// </summary>
internal sealed record KnxCommandBinding(DeviceName Device, CapabilityBinding Capability);
