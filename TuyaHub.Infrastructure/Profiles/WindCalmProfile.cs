using TuyaHub.Application.Commands;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Knx;

namespace TuyaHub.Infrastructure.Profiles;

/// <summary>
/// Device profile #1: the CREATE / IKOHS "Wind Calm" ceiling-fan-with-light. The single place that
/// knows this device's Tuya DP numbers, their raw wire representation, and the KNX DPT choices — all
/// moved here from the old <c>TuyaDatapoints</c>, <c>KnxCommandTranslator</c> and
/// <c>DeviceEventKnxHandler</c>. The domain rules (dim-step, CCT flicker, MCU timer) stay in
/// <see cref="Device"/>, which this profile's aggregate factory builds.
/// </summary>
internal static class WindCalmProfile
{
    public const string ProfileId = "wind-calm";

    // Wind Calm DP numbers (fan_speed DP 62 must be sent as an integer, never a string).
    private const int DpFanPower = 60;
    private const int DpFanSpeed = 62;
    private const int DpFanDirection = 63;
    private const int DpFanTimer = 64;
    private const int DpLightPower = 20;
    private const int DpLightCct = 23;

    private const string DirectionForward = "forward";
    private const string DirectionReverse = "reverse";

    public static DeviceProfile Create() => new()
    {
        ProfileId = ProfileId,
        CreateAggregate = name => new Device(name),
        Capabilities =
        [
            new CapabilityBinding
            {
                Key = WindCalmCapabilities.FanPower,
                Dp = DpFanPower,
                EncodeDp = v => (bool)v,
                DecodeDp = raw => Convert.ToBoolean(raw),
                StatusMappingKey = "FanPowerStatus",
                EncodeStatus = v => KnxDpt.Bool(v.AsBool()),
                CommandMappingKey = "FanPowerCommand",
                BuildCommand = (device, payload) => new SetFanPowerCommand(device, KnxDpt.DecodeBool(payload)),
            },
            new CapabilityBinding
            {
                Key = WindCalmCapabilities.FanSpeed,
                Dp = DpFanSpeed,
                EncodeDp = v => ((SpeedLevel)v).Value, // int, never string
                DecodeDp = raw => Convert.ToInt32(raw) >= 1 ? (object?)SpeedLevel.Clamp(Convert.ToInt32(raw)) : null,
                StatusMappingKey = "FanSpeedStatus",
                EncodeStatus = v => KnxDpt.Count(v.AsInt()),
                CommandMappingKey = "FanSpeedStep",
                BuildCommand = (device, payload) =>
                    KnxDpt.DecodeDimStep(payload) is { } up ? new StepFanSpeedCommand(device, up) : null,
            },
            new CapabilityBinding
            {
                Key = WindCalmCapabilities.FanDirection,
                Dp = DpFanDirection,
                EncodeDp = v => (FanDirection)v == FanDirection.Reverse ? DirectionReverse : DirectionForward,
                DecodeDp = raw => (raw?.ToString() ?? string.Empty).Equals(DirectionReverse, StringComparison.OrdinalIgnoreCase)
                    ? FanDirection.Reverse
                    : FanDirection.Forward,
                StatusMappingKey = "FanDirectionStatus",
                EncodeStatus = v => KnxDpt.Bool(v.AsInt() != 0),
                CommandMappingKey = "FanDirectionCommand",
                BuildCommand = (device, payload) => new SetFanDirectionCommand(
                    device, KnxDpt.DecodeBool(payload) ? FanDirection.Reverse : FanDirection.Forward),
            },
            new CapabilityBinding
            {
                Key = WindCalmCapabilities.FanTimer,
                Dp = DpFanTimer,
                EncodeDp = v => ((CountdownTimer)v).Minutes,
                DecodeDp = raw => CountdownTimer.FromMinutes(Convert.ToInt32(raw)),
                StatusMappingKey = "FanTimerStatus",
                EncodeStatus = v => KnxDpt.Minutes(v.AsInt()),
                CommandMappingKey = "FanTimerCommand",
                BuildCommand = (device, payload) => new SetFanTimerCommand(device, KnxDpt.DecodeMinutes(payload)),
            },
            new CapabilityBinding
            {
                Key = WindCalmCapabilities.LightPower,
                Dp = DpLightPower,
                EncodeDp = v => (bool)v,
                DecodeDp = raw => Convert.ToBoolean(raw),
                StatusMappingKey = "LightPowerStatus",
                EncodeStatus = v => KnxDpt.Bool(v.AsBool()),
                CommandMappingKey = "LightPowerCommand",
                BuildCommand = (device, payload) => new SetLightPowerCommand(device, KnxDpt.DecodeBool(payload)),
            },
            new CapabilityBinding
            {
                Key = WindCalmCapabilities.LightCct,
                Dp = DpLightCct,
                EncodeDp = v => ((ColourTemperature)v).Dp,
                DecodeDp = raw => ColourTemperature.FromDp(Convert.ToInt32(raw)),
                StatusMappingKey = "LightCctStatus",
                EncodeStatus = v => KnxDpt.Percent(v.AsInt()),
                CommandMappingKey = "LightCctCommand",
                BuildCommand = (device, payload) => new SetLightCctCommand(device, KnxDpt.DecodePercent(payload)),
            },
            new CapabilityBinding
            {
                // Availability has no Tuya dps and no command — it is driven by connectivity transitions.
                Key = WellKnownCapabilities.Availability,
                StatusMappingKey = "AvailabilityStatus",
                EncodeStatus = v => KnxDpt.Bool(v.AsBool()),
            },
        ],
    };
}
