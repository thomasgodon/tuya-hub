using System.Collections.Concurrent;
using System.Threading.Channels;

namespace TuyaHub.Application.Dashboard;

internal sealed class DeviceSnapshotBroadcaster : IDeviceSnapshotBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

    public string? Latest { get; private set; }

    public void Publish(string snapshotJson)
    {
        Latest = snapshotJson;

        foreach (var channel in _subscribers.Values)
        {
            // Drop-oldest bounded channel: a slow client never blocks publishing or other clients.
            channel.Writer.TryWrite(snapshotJson);
        }
    }

    public (ChannelReader<string> Reader, IDisposable Subscription) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _subscribers[id] = channel;
        return (channel.Reader, new Subscription(this, id));
    }

    private void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
            channel.Writer.TryComplete();
    }

    private sealed class Subscription(DeviceSnapshotBroadcaster broadcaster, Guid id) : IDisposable
    {
        public void Dispose() => broadcaster.Unsubscribe(id);
    }
}
