using TuyaHub.Application.Commands;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// The inbound half of the KNX ACL: decodes a raw command-GA payload into the matching Application
/// command record for its capability. Decoding and construction only — every rule (dim-step from
/// off, clamp 1..6, % ↔ 0..1000 scaling, timer clamp, 0 % = off) lives in the aggregate and its
/// value objects, which the resulting command flows through. The mirror image of
/// <see cref="DeviceEventKnxHandler"/> on the feedback side.
/// </summary>
internal static class KnxCommandTranslator
{
    /// <summary>
    /// Builds the command for a binding and payload, or <c>null</c> when there is nothing to send
    /// (e.g. a fan-speed break/stop telegram). Kept internal-static so it is unit-testable without a bus.
    /// </summary>
    public static IDeviceCommand? Translate(KnxCommandBinding binding, byte[] payload)
    {
        var device = binding.Device;
        return binding.Capability switch
        {
            CommandCapability.FanPower => new SetFanPowerCommand(device, KnxDpt.DecodeBool(payload)),
            CommandCapability.FanSpeedStep => TranslateFanSpeedStep(device, payload),
            CommandCapability.FanDirection => new SetFanDirectionCommand(
                device, KnxDpt.DecodeBool(payload) ? FanDirection.Reverse : FanDirection.Forward),
            CommandCapability.FanTimer => new SetFanTimerCommand(device, KnxDpt.DecodeMinutes(payload)),
            CommandCapability.LightPower => new SetLightPowerCommand(device, KnxDpt.DecodeBool(payload)),
            CommandCapability.LightBrightness => new SetLightBrightnessCommand(device, KnxDpt.DecodePercent(payload)),
            _ => null,
        };
    }

    private static IDeviceCommand? TranslateFanSpeedStep(DeviceName device, byte[] payload)
    {
        var up = KnxDpt.DecodeDimStep(payload);
        return up is null ? null : new StepFanSpeedCommand(device, up.Value);
    }
}
