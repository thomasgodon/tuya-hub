using TuyaHub.Domain;
using TuyaHub.Infrastructure.Profiles;

namespace TuyaHub.Infrastructure.Tuya;

/// <summary>
/// The Tuya anti-corruption translation, driven by a device's <see cref="DeviceProfile"/> capability
/// table (the single place that knows DP numbers and wire shapes). Converts a domain
/// <see cref="DeviceCommand"/> to a <c>dps</c> dictionary (outbound) and a raw <c>dps</c> reading to a
/// domain <see cref="DeviceReport"/> (inbound). Replaces the old wind-calm-only <c>TuyaDatapoints</c>.
/// </summary>
internal static class TuyaProfileCodec
{
    /// <summary>
    /// Translates a domain command into Tuya datapoints. Only set capabilities with a mapped DP are
    /// included. Integer DPs are emitted as boxed <see cref="int"/> so they serialize as JSON numbers —
    /// e.g. fan speed (DP 62) must never be a string.
    /// </summary>
    public static Dictionary<string, object> ToDps(DeviceProfile profile, DeviceCommand command)
    {
        var dps = new Dictionary<string, object>();

        foreach (var binding in profile.Capabilities)
        {
            if (binding.Dp is not { } dp || binding.EncodeDp is null)
            {
                continue;
            }

            if (command.Values.TryGetValue(binding.Key, out var value))
            {
                dps[dp.ToString()] = binding.EncodeDp(value);
            }
        }

        return dps;
    }

    /// <summary>
    /// Translates a raw datapoint reading (DP id → value) into a domain report. Datapoints without a
    /// binding, or whose decoder returns null, are ignored; absent ones stay absent.
    /// </summary>
    public static DeviceReport ToReport(DeviceProfile profile, IReadOnlyDictionary<int, object> dps)
    {
        var report = new DeviceReport();

        foreach (var binding in profile.Capabilities)
        {
            if (binding.Dp is not { } dp || binding.DecodeDp is null)
            {
                continue;
            }

            if (dps.TryGetValue(dp, out var raw) && binding.DecodeDp(raw) is { } decoded)
            {
                report = report.With(binding.Key, decoded);
            }
        }

        return report;
    }
}
