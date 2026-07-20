using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain;

/// <summary>
/// A set of intended datapoint writes expressed in domain terms. Produced by a device aggregate's
/// command methods and translated into a Tuya <c>dps</c> dictionary by the Tuya ACL — the aggregate
/// never speaks DP numbers. Only the capabilities that are set are written; an <see cref="IsEmpty"/>
/// command means "nothing to send" (e.g. a no-op dim step or an unchanged CCT).
///
/// The currency is generic: values are held in a sparse <see cref="Values"/> bag keyed by
/// <see cref="CapabilityKey"/>, so a new device type sets its capabilities via <see cref="With"/>
/// without editing this type. The typed Wind Calm properties (<see cref="FanPower"/>, …) are a facade
/// over that bag, so existing call sites compile and behave identically.
/// </summary>
public sealed record DeviceCommand
{
    private readonly Dictionary<CapabilityKey, object> _values;

    public DeviceCommand() => _values = new Dictionary<CapabilityKey, object>();

    // Copy constructor so `with` / With() clone the bag instead of sharing it.
    private DeviceCommand(DeviceCommand original)
        => _values = new Dictionary<CapabilityKey, object>(original._values);

    /// <summary>The set capabilities, keyed by capability. Values are domain value objects (boxed).</summary>
    public IReadOnlyDictionary<CapabilityKey, object> Values => _values;

    public static readonly DeviceCommand Empty = new();

    public bool IsEmpty => _values.Count == 0;

    /// <summary>Returns a copy with the given capability set (the generic builder for any device type).</summary>
    public DeviceCommand With(CapabilityKey key, object value)
    {
        var copy = new DeviceCommand(this);
        copy._values[key] = value;
        return copy;
    }

    // ---- Wind Calm typed facade over the capability bag ----
    public bool? FanPower { get => Get<bool>(WindCalmCapabilities.FanPower); init => Set(WindCalmCapabilities.FanPower, value); }
    public SpeedLevel? FanSpeed { get => Get<SpeedLevel>(WindCalmCapabilities.FanSpeed); init => Set(WindCalmCapabilities.FanSpeed, value); }
    public FanDirection? FanDirection { get => Get<FanDirection>(WindCalmCapabilities.FanDirection); init => Set(WindCalmCapabilities.FanDirection, value); }
    public CountdownTimer? FanTimer { get => Get<CountdownTimer>(WindCalmCapabilities.FanTimer); init => Set(WindCalmCapabilities.FanTimer, value); }
    public bool? FanBeep { get => Get<bool>(WindCalmCapabilities.FanBeep); init => Set(WindCalmCapabilities.FanBeep, value); }
    public bool? LightPower { get => Get<bool>(WindCalmCapabilities.LightPower); init => Set(WindCalmCapabilities.LightPower, value); }
    public ColourTemperature? LightCct { get => Get<ColourTemperature>(WindCalmCapabilities.LightCct); init => Set(WindCalmCapabilities.LightCct, value); }

    private T? Get<T>(CapabilityKey key) where T : struct
        => _values.TryGetValue(key, out var value) ? (T)value : null;

    private void Set<T>(CapabilityKey key, T? value) where T : struct
    {
        if (value.HasValue)
        {
            _values[key] = value.Value;
        }
        else
        {
            _values.Remove(key);
        }
    }
}
