using Knx.Falcon;
using Knx.Falcon.Configuration;
using Knx.Falcon.Sdk;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Options;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// The KNX ACL (outbound / feedback direction). Owns the KNXnet/IP tunnelling connection, mirrors
/// device state to the mapped <b>status</b> group addresses, and answers <c>GroupValueRead</c> on
/// those addresses from the last known value. Ports DsmrHub's <c>KnxMeterReadingHandler</c>
/// connection / dedup / read-response behaviour, generalised across N devices × capabilities.
///
/// Command GAs (KNX → Tuya) are not consumed here — the inbound path is M4.
/// </summary>
internal sealed class KnxBridge : IAsyncDisposable
{
    private readonly KnxOptions _options;
    private readonly ILogger<KnxBridge> _logger;
    private readonly Dictionary<(DeviceName Device, Capability Capability), KnxStatusValue> _store;
    private readonly Dictionary<GroupAddress, KnxStatusValue> _byAddress;
    private readonly object _valuesLock = new();
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private KnxBus? _bus;

    public KnxBridge(IOptions<KnxOptions> options, IOptions<DeviceMappingOptions> mappings, ILogger<KnxBridge> logger)
    {
        _options = options.Value;
        _logger = logger;
        _store = BuildStore(mappings.Value);
        _byAddress = _store.Values.ToDictionary(status => status.Address);
    }

    /// <summary>True when the KNX bus is enabled and at least one status GA is mapped.</summary>
    public bool HasWork => _options.Enabled && _store.Count > 0;

    /// <summary>
    /// Establishes the tunnelling connection and subscribes to read requests, so reads are answerable
    /// before the first status write. No-op when the bus is disabled. Robust reconnect/backoff is M5;
    /// here a dropped connection is re-established lazily on the next write/respond.
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_options.Enabled is false)
        {
            _logger.LogInformation("KNX bus disabled; the feedback path is inert.");
            return Task.CompletedTask;
        }

        if (_store.Count == 0)
        {
            _logger.LogWarning("KNX bus enabled but no status group addresses are mapped; nothing to publish.");
            return Task.CompletedTask;
        }

        return EnsureConnectedAsync(cancellationToken);
    }

    /// <summary>
    /// Mirrors a capability's value to its status GA: dedups against the last write (FR-8), caches it
    /// for read responses (FR-7), and writes it to the bus. No-op when the bus is disabled or the
    /// capability has no mapped status GA.
    /// </summary>
    public async Task PublishAsync(DeviceName device, Capability capability, byte[] value, CancellationToken cancellationToken)
    {
        if (_options.Enabled is false)
        {
            return;
        }

        KnxStatusValue status;
        lock (_valuesLock)
        {
            if (_store.TryGetValue((device, capability), out var mapped) is false)
            {
                return; // GA disabled for this device/capability.
            }

            if (mapped.Value is not null && mapped.Value.SequenceEqual(value))
            {
                return; // Unchanged — suppress the redundant write.
            }

            mapped.Value = value;
            status = mapped;
        }

        await WriteAsync(status, cancellationToken);
    }

    private async Task WriteAsync(KnxStatusValue status, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureConnectedAsync(cancellationToken);
            await _bus!.WriteGroupValueAsync(status.Address, new GroupValue(status.Value!), MessagePriority.Low, cancellationToken);
            _logger.LogDebug("KNX write {Status}", status);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "KNX write to {Address} failed.", status.Address);
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_bus?.ConnectionState == BusConnectionState.Connected)
        {
            return;
        }

        await _connectGate.WaitAsync(cancellationToken);
        try
        {
            if (_bus?.ConnectionState == BusConnectionState.Connected)
            {
                return;
            }

            var bus = new KnxBus(new IpTunnelingConnectorParameters(_options.Host, _options.Port));
            bus.GroupMessageReceived += OnGroupMessageReceived;

            _logger.LogInformation("Connecting to KNX interface {Host}:{Port}.", _options.Host, _options.Port);
            await bus.ConnectAsync(cancellationToken);
            await bus.SetInterfaceConfigurationAsync(
                new BusInterfaceConfiguration(IndividualAddress.Parse(_options.IndividualAddress)), cancellationToken);
            _bus = bus;
            _logger.LogInformation("Connected to KNX interface {Host}:{Port}.", _options.Host, _options.Port);
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private async void OnGroupMessageReceived(object? sender, GroupEventArgs e)
    {
        try
        {
            if (e.EventType != GroupEventType.ValueRead)
            {
                return;
            }

            KnxStatusValue? status;
            lock (_valuesLock)
            {
                if (_byAddress.TryGetValue(e.DestinationAddress, out status) is false || status.Value is null)
                {
                    return;
                }
            }

            if (_bus is null)
            {
                return;
            }

            await _bus.RespondGroupValueAsync(status.Address, new GroupValue(status.Value), MessagePriority.Low, CancellationToken.None);
            _logger.LogDebug("KNX read answered {Status}", status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to answer KNX read request on {Address}.", e.DestinationAddress);
        }
    }

    // Internal (not private) so the mapping rules can be unit-tested without a live bus.
    internal static Dictionary<(DeviceName, Capability), KnxStatusValue> BuildStore(DeviceMappingOptions mappings)
    {
        var store = new Dictionary<(DeviceName, Capability), KnxStatusValue>();

        foreach (var (name, mapping) in mappings)
        {
            var device = DeviceName.Create(name);
            foreach (var (capability, ga) in StatusAddresses(mapping))
            {
                if (string.IsNullOrWhiteSpace(ga))
                {
                    continue; // Empty GA disables this capability for this device.
                }

                store[(device, capability)] = new KnxStatusValue(GroupAddress.Parse(ga));
            }
        }

        return store;
    }

    // Status GAs only; command GAs (M4) and Light CCT (M6) are intentionally excluded.
    private static IEnumerable<(Capability, string)> StatusAddresses(DeviceMapping m)
    {
        yield return (Capability.FanPower, m.FanPowerStatus);
        yield return (Capability.FanSpeed, m.FanSpeedStatus);
        yield return (Capability.FanDirection, m.FanDirectionStatus);
        yield return (Capability.FanTimer, m.FanTimerStatus);
        yield return (Capability.LightPower, m.LightPowerStatus);
        yield return (Capability.LightBrightness, m.LightBrightnessStatus);
        yield return (Capability.Availability, m.AvailabilityStatus);
    }

    public async ValueTask DisposeAsync()
    {
        if (_bus is not null)
        {
            _bus.GroupMessageReceived -= OnGroupMessageReceived;
            await _bus.DisposeAsync();
        }

        _connectGate.Dispose();
    }
}
