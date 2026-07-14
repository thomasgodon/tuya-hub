using TuyaHub.Application.Commands;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// The inbound half of the KNX ACL: decodes a raw command-GA payload into the matching Application
/// command record by delegating to the bound profile capability's <c>BuildCommand</c>. Decoding and
/// construction only — every rule (dim-step from off, clamp 1..6, % ↔ 0..1000 scaling, timer clamp,
/// 0 % = off) lives in the aggregate and its value objects, which the resulting command flows through.
/// The mirror image of <see cref="DeviceEventKnxHandler"/> on the feedback side.
/// </summary>
internal static class KnxCommandTranslator
{
    /// <summary>
    /// Builds the command for a binding and payload, or <c>null</c> when there is nothing to send
    /// (e.g. a fan-speed break/stop telegram, or a status-only capability). Kept internal-static so it
    /// is unit-testable without a bus.
    /// </summary>
    public static IDeviceCommand? Translate(KnxCommandBinding binding, byte[] payload)
        => binding.Capability.BuildCommand?.Invoke(binding.Device, payload);
}
