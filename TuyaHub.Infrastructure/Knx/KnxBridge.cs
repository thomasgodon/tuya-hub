using Knx.Falcon;
using Knx.Falcon.Configuration;
using Knx.Falcon.Sdk;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Profiles;

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
    private readonly Dictionary<(DeviceName Device, CapabilityKey Capability), KnxStatusValue> _store;
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
        ConfiguredDeviceProfiles profiles,
        ISender sender,
        ILogger<KnxBridge> logger)
    {
        _options = options.Value;
        _sender = sender;
        _logger = logger;
        _store = BuildStore(mappings.Value, profiles.For);
        _byAddress = _store.Values.ToDictionary(status => status.Address);
        _commandsByAddress = BuildCommandBindings(mappings.Value, profiles.For);
    }

    /// <summary>True when the KNX bus is enabled and at least one status or command GA is mapped.</summary>
    public bool HasWork => _options.Enabled && (_store.Count > 0 || _commandsByAddress.Count > 0);

    /// <summary>True when the tunnelling connection is currently established (for the dashboard KNX pill).</summary>
    public bool IsConnected => _bus?.ConnectionState == BusConnectionState.Connected;

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
    public async Task PublishAsync(DeviceName device, CapabilityKey capability, GroupValue value, CancellationToken cancellationToken)
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

            if (SameValue(mapped.Value, value))
            {
                return; // Unchanged — suppress the redundant write.
            }

            mapped.Value = value;
            status = mapped;
        }

        await WriteAsync(status, cancellationToken);
    }

    /// <summary>
    /// Redundant-write / dedup comparison (FR-8). Compares both the DPT bit size and the payload bytes,
    /// rather than relying on <c>GroupValue.Equals</c>, so a 1-bit and an 8-bit value with the same bytes
    /// are never treated as equal.
    /// </summary>
    private static bool SameValue(GroupValue? cached, GroupValue candidate)
        => cached is not null
           && cached.SizeInBit == candidate.SizeInBit
           && cached.Value.SequenceEqual(candidate.Value);

    private async Task WriteAsync(KnxStatusValue status, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureConnectedAsync(cancellationToken);
            await _bus!.WriteGroupValueAsync(status.Address, status.Value!, MessagePriority.Low, cancellationToken);
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

            var bus = await ConnectBusAsync(cancellationToken);

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

    /// <summary>
    /// Opens a fresh tunnelling connection with the inbound subscription attached. When
    /// <see cref="KnxOptions.IndividualAddress"/> is set we request it as the <b>tunnel source address</b>
    /// (tunneling v2) via the connector parameters — the correct way to give the hub its own individual
    /// address. We deliberately no longer call <c>SetInterfaceConfigurationAsync</c>, which reprogrammed
    /// the interface's <i>own</i> physical address: on a clash with a real device that makes the gateway
    /// reject our <b>outbound</b> telegrams (status writes <i>and</i> read responses), leaving inbound
    /// commands working while feedback and read-answers silently fail. If the gateway is tunneling-v1 only
    /// (requesting an address then fails), we retry letting the gateway assign one, so v1 interfaces keep
    /// working.
    /// </summary>
    private async Task<KnxBus> ConnectBusAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connecting to KNX interface {Host}:{Port}.", _options.Host, _options.Port);

        if (string.IsNullOrWhiteSpace(_options.IndividualAddress) is false)
        {
            try
            {
                return await OpenBusAsync(_options.IndividualAddress, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "KNX connect requesting individual address {Address} failed (gateway may be tunneling-v1 only); retrying with a gateway-assigned address.",
                    _options.IndividualAddress);
            }
        }

        return await OpenBusAsync(individualAddress: null, cancellationToken);
    }

    private async Task<KnxBus> OpenBusAsync(string? individualAddress, CancellationToken cancellationToken)
    {
        var parameters = new IpTunnelingConnectorParameters(_options.Host, _options.Port);
        if (individualAddress is not null)
        {
            // Tunneling-v2 slot address; fall back to any free address if this one is already in use.
            parameters.IndividualAddress = IndividualAddress.Parse(individualAddress);
            parameters.FallbackToAnyIndividualAddress = true;
        }

        var bus = new KnxBus(parameters);
        bus.GroupMessageReceived += OnGroupMessageReceived;
        try
        {
            await bus.ConnectAsync(cancellationToken);
        }
        catch
        {
            bus.GroupMessageReceived -= OnGroupMessageReceived;
            await bus.DisposeAsync();
            throw;
        }

        return bus;
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
            // Inbound group telegrams are rare, so log every one at Information — this is the primary
            // diagnostic for "reads never answered": it shows whether a ValueRead even reaches the hub.
            _logger.LogInformation("KNX inbound {EventType} on {Address}.", e.EventType, e.DestinationAddress);

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
            if (_byAddress.TryGetValue(e.DestinationAddress, out status) is false)
            {
                _logger.LogInformation(
                    "KNX read for {Address} ignored — not a mapped status group address.", e.DestinationAddress);
                return;
            }

            if (status.Value is null)
            {
                _logger.LogInformation(
                    "KNX read for {Address} unanswered — no cached value yet (device has not reported this capability).",
                    e.DestinationAddress);
                return;
            }
        }

        // Capture the bus under no lock — a concurrent reconnect may null the field between here and use.
        var bus = _bus;
        if (bus is null)
        {
            _logger.LogWarning("KNX read for {Address} unanswered — bus not connected.", e.DestinationAddress);
            return;
        }

        await bus.RespondGroupValueAsync(status.Address, status.Value, MessagePriority.Low, CancellationToken.None);
        _logger.LogInformation("KNX read answered {Status}", status);
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
            binding.Capability.Key, binding.Device, e.DestinationAddress);
    }

    // Internal (not private) so the mapping rules can be unit-tested without a live bus. Each device's
    // profile declares which capabilities have a status GA and under which mapping key; the empty-GA
    // (and duplicate-GA, via _byAddress) rules are preserved.
    internal static Dictionary<(DeviceName, CapabilityKey), KnxStatusValue> BuildStore(
        DeviceMappingOptions mappings, Func<DeviceName, DeviceProfile> profileFor)
    {
        var store = new Dictionary<(DeviceName, CapabilityKey), KnxStatusValue>();

        foreach (var (name, mapping) in mappings)
        {
            var device = DeviceName.Create(name);
            foreach (var binding in profileFor(device).Capabilities)
            {
                if (binding.StatusMappingKey is not { } key)
                {
                    continue; // Capability has no status GA.
                }

                if (mapping.TryGetValue(key, out var ga) is false || string.IsNullOrWhiteSpace(ga))
                {
                    continue; // Absent or empty GA disables this capability for this device.
                }

                store[(device, binding.Key)] = new KnxStatusValue(GroupAddress.Parse(ga));
            }
        }

        return store;
    }

    // Internal (not private) so the command-mapping rules can be unit-tested without a live bus.
    internal static Dictionary<GroupAddress, KnxCommandBinding> BuildCommandBindings(
        DeviceMappingOptions mappings, Func<DeviceName, DeviceProfile> profileFor)
    {
        var bindings = new Dictionary<GroupAddress, KnxCommandBinding>();

        foreach (var (name, mapping) in mappings)
        {
            var device = DeviceName.Create(name);
            foreach (var binding in profileFor(device).Capabilities)
            {
                if (binding.CommandMappingKey is not { } key || binding.BuildCommand is null)
                {
                    continue; // Capability accepts no command.
                }

                if (mapping.TryGetValue(key, out var ga) is false || string.IsNullOrWhiteSpace(ga))
                {
                    continue; // Absent or empty GA disables this command for this device.
                }

                bindings[GroupAddress.Parse(ga)] = new KnxCommandBinding(device, binding);
            }
        }

        return bindings;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeBusAsync();
        _connectGate.Dispose();
    }
}
