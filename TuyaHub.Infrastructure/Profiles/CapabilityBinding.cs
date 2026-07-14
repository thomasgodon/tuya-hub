using TuyaHub.Application.Commands;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Infrastructure.Profiles;

/// <summary>
/// One row of a <see cref="DeviceProfile"/>'s capability table: the single place that knows, for one
/// capability of one device type, its Tuya datapoint, its KNX status/command group-address mapping
/// keys, and the codecs that move it across each wire. The generic ACL engines iterate these rows, so a
/// new device type is added by declaring its bindings — no ACL <c>switch</c>/<c>yield</c>/enum is edited.
///
/// Fields are optional per capability: a Tuya-less capability (e.g. availability, derived from
/// connectivity) leaves <see cref="Dp"/> null; a feedback-only capability leaves the command fields null.
/// </summary>
internal sealed record CapabilityBinding
{
    public required CapabilityKey Key { get; init; }

    // ---- Tuya (local dps) ----
    /// <summary>The Tuya datapoint id, or null if this capability has no dps (e.g. availability).</summary>
    public int? Dp { get; init; }

    /// <summary>Domain value (as held in the command bag) → JSON-ready dps value. Ints stay boxed ints.</summary>
    public Func<object, object>? EncodeDp { get; init; }

    /// <summary>Raw dps value → domain value for the report bag; return null to drop the reading.</summary>
    public Func<object, object?>? DecodeDp { get; init; }

    // ---- KNX status (feedback) ----
    /// <summary>The <c>DeviceMapping</c> key of the status GA (e.g. "FanPowerStatus"), or null.</summary>
    public string? StatusMappingKey { get; init; }

    /// <summary>Encodes the capability's scalar to its status GA payload (chooses the DPT).</summary>
    public Func<CapabilityValue, byte[]>? EncodeStatus { get; init; }

    // ---- KNX command (inbound) ----
    /// <summary>The <c>DeviceMapping</c> key of the command GA (e.g. "FanPowerCommand"), or null.</summary>
    public string? CommandMappingKey { get; init; }

    /// <summary>Decodes an inbound command-GA payload into the Application command, or null for a no-op.</summary>
    public Func<DeviceName, byte[], IDeviceCommand?>? BuildCommand { get; init; }

    // ---- Dashboard ----
    /// <summary>
    /// How the capability-driven dashboard renders this capability, or null to omit it. Wind Calm leaves
    /// this null (it has a bespoke fan+light card); other device types set it so the generic renderer can
    /// show a labelled value from the capability snapshot.
    /// </summary>
    public DashboardField? Dashboard { get; init; }
}

/// <summary>A capability's presentation on the generic dashboard: a label and an optional unit suffix.</summary>
internal sealed record DashboardField(string Label, string? Unit = null);
