using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TuyaHub.Application.Abstractions;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Options;

namespace TuyaHub.Infrastructure.Tuya;

/// <summary>
/// Owns one <see cref="TuyaConnection"/> per enabled device, built from configuration. Shared between
/// the gateway (which sends commands) and the supervisor (which runs the connection lifetimes).
/// </summary>
internal sealed class TuyaConnectionManager
{
    private readonly Dictionary<DeviceName, TuyaConnection> _connections;

    public TuyaConnectionManager(
        IOptions<TuyaOptions> options,
        IDeviceStateIngestionService ingestion,
        ILoggerFactory loggerFactory)
    {
        var tuya = options.Value;
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, tuya.PollIntervalSeconds));
        var heartbeatInterval = TimeSpan.FromSeconds(Math.Max(1, tuya.HeartbeatIntervalSeconds));
        var logger = loggerFactory.CreateLogger(typeof(TuyaConnection).FullName!);

        _connections = new Dictionary<DeviceName, TuyaConnection>();
        foreach (var device in tuya.Devices.Where(d => d.Enabled))
        {
            var name = DeviceName.Create(device.Name);
            _connections[name] = new TuyaConnection(name, device, pollInterval, heartbeatInterval, ingestion, logger);
        }
    }

    public IReadOnlyCollection<TuyaConnection> Connections => _connections.Values;

    public TuyaConnection? Get(DeviceName name) => _connections.GetValueOrDefault(name);
}
