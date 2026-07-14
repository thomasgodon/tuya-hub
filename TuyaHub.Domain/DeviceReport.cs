using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain;

/// <summary>
/// An observed snapshot of device state, translated from a Tuya <c>dps</c> update by the Tuya ACL into
/// domain terms and fed to <see cref="IDevice.ApplyReportedState"/> (device readback is authoritative).
/// A capability absent from <see cref="Values"/> was not present in this update (partial updates are
/// normal for pushed status).
///
/// Like <see cref="DeviceCommand"/> the currency is a sparse <see cref="CapabilityKey"/> bag; the typed
/// Wind Calm properties are a facade over it (a <c>null</c> facade value means the capability is absent).
/// </summary>
public sealed record DeviceReport
{
    private readonly Dictionary<CapabilityKey, object> _values;

    public DeviceReport() => _values = new Dictionary<CapabilityKey, object>();

    private DeviceReport(DeviceReport original)
        => _values = new Dictionary<CapabilityKey, object>(original._values);

    /// <summary>The reported capabilities, keyed by capability. Values are domain value objects (boxed).</summary>
    public IReadOnlyDictionary<CapabilityKey, object> Values => _values;

    /// <summary>Returns a copy with the given capability set (the generic builder for any device type).</summary>
    public DeviceReport With(CapabilityKey key, object value)
    {
        var copy = new DeviceReport(this);
        copy._values[key] = value;
        return copy;
    }

    // ---- Wind Calm typed facade over the capability bag ----
    public bool? FanPower { get => Get<bool>(WindCalmCapabilities.FanPower); init => Set(WindCalmCapabilities.FanPower, value); }
    public SpeedLevel? FanSpeed { get => Get<SpeedLevel>(WindCalmCapabilities.FanSpeed); init => Set(WindCalmCapabilities.FanSpeed, value); }
    public FanDirection? FanDirection { get => Get<FanDirection>(WindCalmCapabilities.FanDirection); init => Set(WindCalmCapabilities.FanDirection, value); }
    public CountdownTimer? FanTimer { get => Get<CountdownTimer>(WindCalmCapabilities.FanTimer); init => Set(WindCalmCapabilities.FanTimer, value); }
    public bool? LightPower { get => Get<bool>(WindCalmCapabilities.LightPower); init => Set(WindCalmCapabilities.LightPower, value); }
    public Brightness? LightBrightness { get => Get<Brightness>(WindCalmCapabilities.LightBrightness); init => Set(WindCalmCapabilities.LightBrightness, value); }
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
