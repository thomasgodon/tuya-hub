using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Application.Abstractions;

/// <summary>
/// Entry point for the feedback path: the Tuya ACL calls this when it observes device state (pushed
/// status or a poll) or a connectivity transition. The service applies the observation to the
/// aggregate and publishes the resulting domain events (which the KNX ACL turns into status telegrams).
/// This is the tuya-hub analogue of DsmrHub's <c>TelegramIngestionService</c>.
/// </summary>
public interface IDeviceStateIngestionService
{
    /// <summary>Applies an observed device snapshot and publishes any resulting change events.</summary>
    Task ReportStateAsync(DeviceName device, DeviceReport report, CancellationToken cancellationToken);

    /// <summary>Reports a connectivity transition (online/offline) and publishes any resulting event.</summary>
    Task ReportConnectivityAsync(DeviceName device, bool online, CancellationToken cancellationToken);
}
