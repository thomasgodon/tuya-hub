namespace TuyaHub.Infrastructure.Resilience;

/// <summary>
/// Exponential reconnect backoff shared by the Tuya and KNX supervision loops (M5). The base schedule
/// doubles from <c>initial</c> toward the <c>max</c> cap and resets on a successful connect; each read
/// applies ±<see cref="_jitterFraction"/> jitter so that N devices (and the KNX bus) dropped by the
/// same event — e.g. a gateway restart — do not reconnect in lock-step and hammer the network.
///
/// The base schedule (doubling + cap) is deterministic and advances independently of the jitter, so it
/// is unit-testable with jitter disabled while the jitter is verifiable by bounds.
/// </summary>
internal sealed class BackoffPolicy
{
    private readonly TimeSpan _initial;
    private readonly TimeSpan _max;
    private readonly double _jitterFraction;
    private readonly Func<double> _random;
    private TimeSpan _current;

    /// <param name="initial">First (and post-reset) delay. Floored at 1 tick.</param>
    /// <param name="max">Upper cap for the doubling base. Raised to <paramref name="initial"/> if smaller.</param>
    /// <param name="jitterFraction">Symmetric jitter as a fraction of the base delay (e.g. 0.2 = ±20%). Clamped to [0, 1).</param>
    /// <param name="random">Source of a value in [0, 1); defaults to <see cref="Random.Shared"/>. Inject for deterministic tests.</param>
    public BackoffPolicy(TimeSpan initial, TimeSpan max, double jitterFraction = 0.2, Func<double>? random = null)
    {
        _initial = initial.Ticks < 1 ? TimeSpan.FromTicks(1) : initial;
        _max = max < _initial ? _initial : max;
        _jitterFraction = Math.Clamp(jitterFraction, 0d, 0.999d);
        _random = random ?? Random.Shared.NextDouble;
        _current = _initial;
    }

    /// <summary>Returns the current delay (with jitter applied), then doubles the base toward the cap.</summary>
    public TimeSpan Next()
    {
        var jittered = ApplyJitter(_current);
        _current = TimeSpan.FromTicks(Math.Min(_max.Ticks, _current.Ticks * 2));
        return jittered;
    }

    /// <summary>Resets the base delay to <c>initial</c>; call after a successful connect.</summary>
    public void Reset() => _current = _initial;

    private TimeSpan ApplyJitter(TimeSpan value)
    {
        if (_jitterFraction <= 0d)
        {
            return value;
        }

        // factor in (1 - f, 1 + f) — always positive, so the delay never goes negative.
        var factor = 1d + (_random() * 2d - 1d) * _jitterFraction;
        return TimeSpan.FromTicks((long)(value.Ticks * factor));
    }
}
