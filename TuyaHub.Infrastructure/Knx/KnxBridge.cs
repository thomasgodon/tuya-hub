using Knx.Falcon;
using Knx.Falcon.Configuration;
using Knx.Falcon.Sdk;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Options;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// The KNX ACL. Owns the KNXnet/IP tunnelling connection and translates both directions:
/// <list type="bullet">
/// <item>feedback — mirrors device state to the mapped <b>status</b> group addresses and answers
/// <c>GroupValueRead</c> on those addresses from the last known value;</item>
/// <item>command — decodes an inbound <c>GroupValueWrite</c> on a mapped <b>command</b> group address
/// into the matching domain command and dispatches it via MediatR (KNX → Tuya).</item>
/// </list>
/// Ports DsmrHub's <c>KnxMeterReadingHandler</c> connection / dedup / read-response behaviour,
/// generalised across N devices × capabilities.
/// </summary>
internal sealed class KnxBridge : IAsyncDisposable
{
    private readonly KnxOptions _options;
    private readonly ISender _sender;
    private readonly ILogger<KnxBridge> _logger;
    private readonly Dictionary<(DeviceName Device, Capability Capability), KnxStatusValue> _store;
    private readonly Dictionary<GroupAddress, KnxStatusValue> _byAddress;
    private readonly Dictionary<GroupAddress, KnxCommandBinding> _commandsByAddress;
    private readonly object _valuesLock = new();
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private KnxBus? _bus;

    /// <summary>Completes when the current bus drops; recreated on each successful connect (M5 reconnect signal).</summary>
    private TaskCompletionSource _dropSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public KnxBridge(
        IOptions<KnxOptions> options,
        IOptions<DeviceMappingOptions> mappings,
        ISender sender,
        ILogger<KnxBridge> logger)
    {
        _options = options.Value;
        _sender = sender;
        _logger = logger;
        _store = BuildStore(mappings.Value);
        _byAddress = _store.Values.ToDictionary(status => status.Address);
        _commandsByAddress = BuildCommandBindings(mappings.Value);
    }

    /// <summary>True when the KNX bus is enabled and at least one status or command GA is mapped.</summary>
    public bool HasWork => _options.Enabled && (_store.Count > 0 || _commandsByAddress.Count > 0);

    /// <summary>
    /// Establishes the tunnelling connection and subscribes to group messages, so read requests are
    /// answerable before the first status write and command writes are received from startup. No-op
    /// when the bus is disabled. <see cref="KnxConnectionSupervisor"/> drives robust reconnect (M5) by
    /// calling this after each <see cref="WaitForDropAsync"/>; writes/responds also re-establish it lazily.
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_options.Enabled is false)
        {
            _logger.LogInformation("KNX bus disabled; the feedback path is inert.");
            return Task.CompletedTask;
        }

        if (_store.Count == 0 && _commandsByAddress.Count == 0)
        {
            _logger.LogWarning("KNX bus enabled but no group addresses are mapped; nothing to publish or command.");
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

    /// <summary>
    /// Completes when the current KNX connection drops (state leaves <see cref="BusConnectionState.Connected"/>),
    /// or is cancelled on shutdown. The supervisor awaits this, then reconnects with backoff (M5).
    /// </summary>
    public Task WaitForDropAsync(CancellationToken cancellationToken) => _dropSignal.Task.WaitAsync(cancellationToken);

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

            // Tear down a stale/broken bus before opening a fresh one — never hold two sockets.
            await DisposeBusAsync();

            var bus = new KnxBus(new IpTunnelingConnectorParameters(_options.Host, _options.Port));
            bus.GroupMessageReceived += OnGroupMessageReceived;

            _logger.LogInformation("Connecting to KNX interface {Host}:{Port}.", _options.Host, _options.Port);
            await bus.ConnectAsync(cancellationToken);
            await bus.SetInterfaceConfigurationAsync(
                new BusInterfaceConfiguration(IndividualAddress.Parse(_options.IndividualAddress)), cancellationToken);

            // Fresh drop signal for this connection, then start watching for drops.
            _dropSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bus.ConnectionStateChanged += OnConnectionStateChanged;
            _bus = bus;
            _logger.LogInformation("Connected to KNX interface {Host}:{Port}.", _options.Host, _options.Port);
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        // Keyed off the sender (not _bus) to avoid racing the _bus assignment inside EnsureConnectedAsync.
        if (sender is KnxBus bus && bus.ConnectionState != BusConnectionState.Connected)
        {
            _logger.LogWarning("KNX bus dropped (state {State}); reconnect pending.", bus.ConnectionState);
            _dropSignal.TrySetResult();
        }
    }

    private async Task DisposeBusAsync()
    {
        if (_bus is null)
        {
            return;
        }

        _bus.GroupMessageReceived -= OnGroupMessageReceived;
        _bus.ConnectionStateChanged -= OnConnectionStateChanged;
        try
        {
            await _bus.DisposeAsync();
        }
        catch
        {
            // best-effort teardown
        }

        _bus = null;
    }

    private async void OnGroupMessageReceived(object? sender, GroupEventArgs e)
    {
        try
        {
            switch (e.EventType)
            {
                case GroupEventType.ValueRead:
                    await AnswerReadAsync(e);
                    break;
                case GroupEventType.ValueWrite:
                    await DispatchCommandAsync(e);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to handle KNX {EventType} on {Address}.", e.EventType, e.DestinationAddress);
        }
    }

    private async Task AnswerReadAsync(GroupEventArgs e)
    {
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

    private async Task DispatchCommandAsync(GroupEventArgs e)
    {
        if (_commandsByAddress.TryGetValue(e.DestinationAddress, out var binding) is false)
        {
            return; // Not a mapped command GA (e.g. a write to a status GA) — ignore.
        }

        if (e.Value is null)
        {
            _logger.LogWarning("KNX command on {Address} carried no value.", e.DestinationAddress);
            return;
        }

        var command = KnxCommandTranslator.Translate(binding, e.Value.Value);
        if (command is null)
        {
            return; // Nothing to send (e.g. a fan-speed break/stop telegram).
        }

        await _sender.Send(command, CancellationToken.None);
        _logger.LogDebug("KNX command {Capability} for {Device} dispatched from {Address}.",
            binding.Capability, binding.Device, e.DestinationAddress);
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

    // Status GAs only; command GAs are handled by BuildCommandBindings and Light CCT (M6) is excluded.
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

    // Internal (not private) so the command-mapping rules can be unit-tested without a live bus.
    internal static Dictionary<GroupAddress, KnxCommandBinding> BuildCommandBindings(DeviceMappingOptions mappings)
    {
        var bindings = new Dictionary<GroupAddress, KnxCommandBinding>();

        foreach (var (name, mapping) in mappings)
        {
            var device = DeviceName.Create(name);
            foreach (var (capability, ga) in CommandAddresses(mapping))
            {
                if (string.IsNullOrWhiteSpace(ga))
                {
                    continue; // Empty GA disables this command for this device.
                }

                bindings[GroupAddress.Parse(ga)] = new KnxCommandBinding(device, capability);
            }
        }

        return bindings;
    }

    // Command GAs only; the Light CCT command (M6) is intentionally excluded.
    private static IEnumerable<(CommandCapability, string)> CommandAddresses(DeviceMapping m)
    {
        yield return (CommandCapability.FanPower, m.FanPowerCommand);
        yield return (CommandCapability.FanSpeedStep, m.FanSpeedStep);
        yield return (CommandCapability.FanDirection, m.FanDirectionCommand);
        yield return (CommandCapability.FanTimer, m.FanTimerCommand);
        yield return (CommandCapability.LightPower, m.LightPowerCommand);
        yield return (CommandCapability.LightBrightness, m.LightBrightnessCommand);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeBusAsync();
        _connectGate.Dispose();
    }
}
