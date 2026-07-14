using System.Threading.Channels;

namespace TuyaHub.Application.Dashboard;

/// <summary>
/// In-memory pub/sub for the latest dashboard snapshot (serialized JSON). The dashboard notification
/// handler publishes on every device state change; SSE connections subscribe to receive pushes.
/// </summary>
public interface IDeviceSnapshotBroadcaster
{
    /// <summary>The most recently published snapshot JSON, or null if none has been published yet.</summary>
    string? Latest { get; }

    /// <summary>Stores the snapshot as <see cref="Latest"/> and fans it out to all subscribers.</summary>
    void Publish(string snapshotJson);

    /// <summary>
    /// Registers a subscriber. Returns a reader that yields each published snapshot, and a disposable
    /// that removes the subscription (dispose when the SSE connection closes).
    /// </summary>
    (ChannelReader<string> Reader, IDisposable Subscription) Subscribe();
}
