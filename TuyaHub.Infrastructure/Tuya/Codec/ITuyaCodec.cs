using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Infrastructure.Tuya.Codec;

/// <summary>
/// The Tuya wire codec seam. <see cref="TuyaConnection"/> owns the socket and reliability layer and is
/// otherwise transport-only; everything version-specific (framing, encryption, integrity, and — for
/// 3.4/3.5 — the session-key handshake) lives behind this interface. Two implementations:
/// <see cref="TuyaNetCodec"/> (3.1/3.3, delegating to the pinned TuyaNet library) and
/// <see cref="TuyaSessionCodec"/> (hand-rolled 3.4/3.5).
/// </summary>
internal interface ITuyaCodec
{
    ProtocolVersion Version { get; }

    /// <summary>
    /// Whether <see cref="TuyaConnection"/> should run the periodic heartbeat loop for this codec.
    /// Codecs whose keepalive/liveness is already carried by the <c>DP_QUERY</c> poll return <c>false</c>
    /// — the hand-rolled 3.4/3.5 <c>HEART_BEAT</c> frame drops the socket on some firmware (a 3.5 unit
    /// closes the connection on receipt) and adds nothing the poll does not. Only TuyaNet's
    /// library-proven 3.1/3.3 heartbeat opts in.
    /// </summary>
    bool UsesHeartbeat { get; }

    /// <summary>
    /// Performs the 3.4/3.5 session-key negotiation on a freshly-connected stream, before any DP traffic.
    /// A no-op for 3.1/3.3. Must be called on every (re)connect — the session key is per-connection.
    /// </summary>
    Task NegotiateSessionAsync(Stream stream, CancellationToken cancellationToken);

    /// <summary>Builds a CONTROL frame writing the given datapoints.</summary>
    byte[] BuildControl(IReadOnlyDictionary<string, object> dps);

    /// <summary>Builds a DP_QUERY frame requesting a full status read.</summary>
    byte[] BuildQuery();

    /// <summary>Builds a heartbeat keepalive frame.</summary>
    byte[] BuildHeartbeat();

    /// <summary>
    /// Pulls at most one complete frame out of the rolling receive buffer. Returns <c>true</c> when a
    /// frame was consumed — <paramref name="json"/> is its decoded payload, or <c>null</c> for an
    /// acknowledgement / undecodable frame — and <c>false</c> when the buffer holds no complete frame.
    /// </summary>
    bool TryReadMessage(List<byte> buffer, out string? json);
}
