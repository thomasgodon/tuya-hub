using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Infrastructure.Profiles;

/// <summary>
/// Capability keys common to every device type (not tied to a Tuya datapoint). Availability is derived
/// from connectivity transitions, so it is shared here rather than declared per profile's domain model.
/// </summary>
internal static class WellKnownCapabilities
{
    public static readonly CapabilityKey Availability = new("Availability");
}
