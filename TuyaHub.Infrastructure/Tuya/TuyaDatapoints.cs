using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Infrastructure.Tuya;

/// <summary>
/// The Tuya anti-corruption translation: the single place that knows Wind Calm DP numbers and their
/// raw wire representation. Converts a domain <see cref="DeviceCommand"/> to a <c>dps</c> dictionary
/// (outbound) and a raw <c>dps</c> reading to a domain <see cref="DeviceReport"/> (inbound).
/// </summary>
internal static class TuyaDatapoints
{
    public const int FanPower = 60;
    public const int FanSpeed = 62;
    public const int FanDirection = 63;
    public const int FanTimer = 64;
    public const int LightPower = 20;
    public const int LightBrightness = 22;
    public const int LightCct = 23;

    public const string DirectionForward = "forward";
    public const string DirectionReverse = "reverse";

    /// <summary>
    /// Translates a domain command into Tuya datapoints. Only set fields are included. Integer DPs
    /// (speed, brightness, timer, CCT) are emitted as boxed <see cref="int"/> so they serialize as
    /// JSON numbers — DP 62 must never be a string.
    /// </summary>
    public static Dictionary<string, object> ToDps(DeviceCommand command)
    {
        var dps = new Dictionary<string, object>();

        if (command.FanPower is { } fanPower)
        {
            dps[FanPower.ToString()] = fanPower;
        }

        if (command.FanSpeed is { } fanSpeed)
        {
            dps[FanSpeed.ToString()] = fanSpeed.Value; // int, never string
        }

        if (command.FanDirection is { } direction)
        {
            dps[FanDirection.ToString()] = direction == Domain.ValueObjects.FanDirection.Reverse
                ? DirectionReverse
                : DirectionForward;
        }

        if (command.FanTimer is { } timer)
        {
            dps[FanTimer.ToString()] = timer.Minutes;
        }

        if (command.LightPower is { } lightPower)
        {
            dps[LightPower.ToString()] = lightPower;
        }

        if (command.LightBrightness is { } brightness)
        {
            dps[LightBrightness.ToString()] = brightness.Dp;
        }

        if (command.LightCct is { } cct)
        {
            dps[LightCct.ToString()] = cct.Dp;
        }

        return dps;
    }

    /// <summary>
    /// Translates a raw datapoint reading (DP id → value, as decoded from the device JSON) into a
    /// domain report. Unknown or unparsable datapoints are ignored; absent ones stay null.
    /// </summary>
    public static DeviceReport ToReport(IReadOnlyDictionary<int, object> dps)
    {
        return new DeviceReport
        {
            FanPower = TryBool(dps, FanPower),
            FanSpeed = TryInt(dps, FanSpeed) is { } speed and >= 1 ? SpeedLevel.Clamp(speed) : null,
            FanDirection = TryString(dps, FanDirection) is { } dir
                ? (dir.Equals(DirectionReverse, StringComparison.OrdinalIgnoreCase)
                    ? Domain.ValueObjects.FanDirection.Reverse
                    : Domain.ValueObjects.FanDirection.Forward)
                : null,
            FanTimer = TryInt(dps, FanTimer) is { } minutes ? CountdownTimer.FromMinutes(minutes) : null,
            LightPower = TryBool(dps, LightPower),
            LightBrightness = TryInt(dps, LightBrightness) is { } bright ? Brightness.FromDp(bright) : null,
            LightCct = TryInt(dps, LightCct) is { } temp ? ColourTemperature.FromDp(temp) : null,
        };
    }

    private static bool? TryBool(IReadOnlyDictionary<int, object> dps, int dp)
        => dps.TryGetValue(dp, out var value) ? Convert.ToBoolean(value) : null;

    private static int? TryInt(IReadOnlyDictionary<int, object> dps, int dp)
        => dps.TryGetValue(dp, out var value) ? Convert.ToInt32(value) : null;

    private static string? TryString(IReadOnlyDictionary<int, object> dps, int dp)
        => dps.TryGetValue(dp, out var value) ? value?.ToString() : null;
}
