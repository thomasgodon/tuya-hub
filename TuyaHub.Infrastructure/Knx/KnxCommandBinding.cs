using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// Binds one inbound command group address to the device and capability it drives. The command-GA
/// lookup maps a <see cref="Knx.Falcon.GroupAddress"/> to this record so an incoming
/// <c>GroupValueWrite</c> can be routed and decoded (the inbound counterpart of
/// <see cref="KnxStatusValue"/>).
/// </summary>
internal sealed record KnxCommandBinding(DeviceName Device, CommandCapability Capability);
