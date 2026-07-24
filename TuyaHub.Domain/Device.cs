using MediatR;
using TuyaHub.Domain.Events;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain;

/// <summary>
/// Aggregate root for a single Wind Calm unit and the consistency boundary for its two endpoints
/// (<see cref="Fan"/> and <see cref="Light"/>). All state-transition rules and firmware quirks live
/// here, expressed in domain terms only — the aggregate never sees a Tuya <c>dps</c> dict or a KNX
/// telegram.
///
/// Two directions meet here:
/// <list type="bullet">
/// <item><b>Command path (KNX → device):</b> the command methods (<see cref="SetFanPower"/>,
/// <see cref="SetFanSpeedPercent"/>, …) are pure — they read current known state and return the intended
/// <see cref="DeviceCommand"/> without mutating state or raising events. This guarantees the bridge
/// never echoes an <i>unconfirmed</i> command back to KNX.</item>
/// <item><b>Feedback path (device → KNX):</b> <see cref="ApplyReportedState"/> is the sole state
/// mutator. Device readback is authoritative; it updates state and returns the change events that
/// drive the KNX status group addresses.</item>
/// </list>
/// </summary>
public sealed class Device : IDevice
{
    private readonly object _gate = new();
    private bool _hasReported;

    public Device(DeviceName name)
    {
        Name = name;
        Fan = new FanEndpoint();
        Light = new LightEndpoint();
    }

    public DeviceName Name { get; }
    public FanEndpoint Fan { get; }
    public LightEndpoint Light { get; }
    public bool IsOnline { get; private set; }

    /// <summary>
    /// Takes a consistent snapshot of the aggregate's current state for read-only consumers (the web
    /// dashboard). Computed under <c>_gate</c> — the same lock every mutator holds — so a concurrent
    /// reader never observes a value the endpoint getters are mid-write.
    /// </summary>
    public DeviceStateSnapshot CaptureState()
    {
        lock (_gate)
        {
            return new DeviceStateSnapshot(
                Name,
                IsOnline,
                Fan.Power,
                Fan.SpeedStatus,
                Fan.Direction,
                Fan.Timer.Minutes,
                Fan.Timer.IsRunning,
                Light.Power,
                Light.ColourTemperature.Dp,
                Light.ColourTemperature.ToPercent());
        }
    }

    // ---- Command path (pure: compute the intended write from current known state) ----

    /// <summary>Fan on/off (DP 60). UC-01: turning on does not force a speed — the device restores its last level.</summary>
    public DeviceCommand SetFanPower(bool on)
    {
        lock (_gate)
        {
            return new DeviceCommand { FanPower = on };
        }
    }

    /// <summary>
    /// Absolute fan speed from a KNX percentage (DPT 5.001, UC-02). <c>0 %</c> turns the fan off;
    /// <c>1..100 %</c> maps onto levels 1..6 (see <see cref="SpeedLevel.FromPercent"/>) and turns the
    /// fan on if it was off. Returns <see cref="DeviceCommand.Empty"/> when nothing needs to change.
    /// </summary>
    public DeviceCommand SetFanSpeedPercent(int percent)
    {
        lock (_gate)
        {
            if (percent <= 0)
            {
                // 0 %: switch off (nothing to send if already off — the level is left untouched).
                return Fan.Power ? new DeviceCommand { FanPower = false } : DeviceCommand.Empty;
            }

            var target = SpeedLevel.FromPercent(percent);
            if (!Fan.Power)
            {
                // Setting a speed while off turns the fan on at that level.
                return new DeviceCommand { FanPower = true, FanSpeed = target };
            }

            return target == Fan.Speed
                ? DeviceCommand.Empty            // already at that level: nothing to send
                : new DeviceCommand { FanSpeed = target };
        }
    }

    /// <summary>Fan direction / summer-winter (DP 63). Takes effect even while the fan is off (UC-03a).</summary>
    public DeviceCommand SetFanDirection(FanDirection direction)
    {
        lock (_gate)
        {
            return new DeviceCommand { FanDirection = direction };
        }
    }

    /// <summary>Set/cancel the auto-off countdown (DP 64). The MCU owns the countdown (UC-04).</summary>
    public DeviceCommand SetFanTimer(CountdownTimer timer)
    {
        lock (_gate)
        {
            return new DeviceCommand { FanTimer = timer };
        }
    }

    /// <summary>Light on/off (DP 20). UC-05.</summary>
    public DeviceCommand SetLightPower(bool on)
    {
        lock (_gate)
        {
            return new DeviceCommand { LightPower = on };
        }
    }

    /// <summary>
    /// Light colour temperature (DP 23, UC-07). Written only on an actual step change to mitigate the
    /// firmware flicker bug — an unchanged value produces an empty command.
    /// </summary>
    public DeviceCommand SetLightColourTemperature(ColourTemperature colourTemperature)
    {
        lock (_gate)
        {
            if (_hasReported && Light.ColourTemperature == colourTemperature)
            {
                return DeviceCommand.Empty;
            }

            return new DeviceCommand { LightCct = colourTemperature };
        }
    }

    /// <summary>
    /// Relative colour-temperature step from a KNX dim telegram (UC-07) — a long-press cycles
    /// 0 → 500 → 1000 → 0 …, wrapping at the rails. Independent of light power (DP 20). With three
    /// distinct steps the target always differs from the current step, but the flicker guard is kept
    /// for symmetry with <see cref="SetLightColourTemperature"/>.
    /// </summary>
    public DeviceCommand CycleLightColourTemperature(bool up)
    {
        lock (_gate)
        {
            var target = Light.ColourTemperature.Cycle(up);
            return _hasReported && Light.ColourTemperature == target
                ? DeviceCommand.Empty
                : new DeviceCommand { LightCct = target };
        }
    }

    // ---- Feedback path (authoritative: mutate state, emit change events) ----

    /// <summary>
    /// Applies an observed device snapshot (from pushed status or a poll). Device readback is
    /// authoritative. Returns the change events to publish; on the first ever report every mapped
    /// value is emitted so the KNX status GAs are fully synced on startup (UC-08 08c).
    /// </summary>
    public IReadOnlyList<INotification> ApplyReportedState(DeviceReport report)
    {
        lock (_gate)
        {
            var events = new List<INotification>();
            var full = !_hasReported;

            // Fan power + speed together drive the single 5.010 speed status (0 = off).
            var oldSpeedStatus = Fan.SpeedStatus;
            var fanPowerChanged = false;

            if (report.FanPower is { } fanPower && (full || Fan.Power != fanPower))
            {
                Fan.Power = fanPower;
                fanPowerChanged = true;
            }

            if (report.FanSpeed is { } fanSpeed)
            {
                Fan.Speed = fanSpeed;
            }

            if (fanPowerChanged)
            {
                Raise(events, WindCalmCapabilities.FanPower, CapabilityValue.Bool(Fan.Power));
            }

            if (full || Fan.SpeedStatus != oldSpeedStatus)
            {
                Raise(events, WindCalmCapabilities.FanSpeed, CapabilityValue.Int(Fan.SpeedStatus));
            }

            if (report.FanDirection is { } direction && (full || Fan.Direction != direction))
            {
                Fan.Direction = direction;
                Raise(events, WindCalmCapabilities.FanDirection, CapabilityValue.Int((int)direction));
            }

            if (report.FanTimer is { } timer && (full || Fan.Timer != timer))
            {
                Fan.Timer = timer;
                Raise(events, WindCalmCapabilities.FanTimer, CapabilityValue.Int(timer.Minutes));
            }

            if (report.LightPower is { } lightPower && (full || Light.Power != lightPower))
            {
                Light.Power = lightPower;
                Raise(events, WindCalmCapabilities.LightPower, CapabilityValue.Bool(lightPower));
            }

            if (report.LightCct is { } cct && (full || Light.ColourTemperature != cct))
            {
                Light.ColourTemperature = cct;
                Raise(events, WindCalmCapabilities.LightCct, CapabilityValue.Int(cct.ToPercent()));
            }

            _hasReported = true;
            return events;
        }
    }

    private void Raise(List<INotification> events, CapabilityKey capability, CapabilityValue value)
        => events.Add(new DeviceCapabilityChanged(Name, capability, value));

    /// <summary>Marks the device unreachable. Idempotent — emits <see cref="DeviceWentOffline"/> only on transition.</summary>
    public IReadOnlyList<INotification> MarkOffline()
    {
        lock (_gate)
        {
            if (!IsOnline)
            {
                return [];
            }

            IsOnline = false;
            return [new DeviceWentOffline(Name)];
        }
    }

    /// <summary>Marks the device reachable. Idempotent — emits <see cref="DeviceReconnected"/> only on transition.</summary>
    public IReadOnlyList<INotification> MarkReconnected()
    {
        lock (_gate)
        {
            if (IsOnline)
            {
                return [];
            }

            IsOnline = true;
            return [new DeviceReconnected(Name)];
        }
    }
}
