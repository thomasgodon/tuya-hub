using TuyaHub.Infrastructure.Resilience;
using Xunit;

namespace TuyaHub.Tests.Resilience;

public class BackoffPolicyTests
{
    // Jitter disabled so the base schedule is deterministic.
    private static BackoffPolicy NoJitter(double initialSeconds, double maxSeconds)
        => new(TimeSpan.FromSeconds(initialSeconds), TimeSpan.FromSeconds(maxSeconds), jitterFraction: 0d);

    [Fact]
    public void Next_doubles_the_base_delay_toward_the_cap()
    {
        var backoff = NoJitter(1, 30);

        Assert.Equal(1, backoff.Next().TotalSeconds, 3);
        Assert.Equal(2, backoff.Next().TotalSeconds, 3);
        Assert.Equal(4, backoff.Next().TotalSeconds, 3);
        Assert.Equal(8, backoff.Next().TotalSeconds, 3);
        Assert.Equal(16, backoff.Next().TotalSeconds, 3);
    }

    [Fact]
    public void Next_never_exceeds_the_cap()
    {
        var backoff = NoJitter(1, 30);

        // 1, 2, 4, 8, 16, then capped at 30 (32 would exceed).
        for (var i = 0; i < 5; i++)
        {
            backoff.Next();
        }

        Assert.Equal(30, backoff.Next().TotalSeconds, 3);
        Assert.Equal(30, backoff.Next().TotalSeconds, 3);
    }

    [Fact]
    public void Reset_returns_to_the_initial_delay()
    {
        var backoff = NoJitter(1, 30);

        backoff.Next(); // 1
        backoff.Next(); // 2
        backoff.Next(); // 4
        backoff.Reset();

        Assert.Equal(1, backoff.Next().TotalSeconds, 3);
        Assert.Equal(2, backoff.Next().TotalSeconds, 3);
    }

    [Fact]
    public void Max_is_raised_to_initial_when_smaller()
    {
        var backoff = new BackoffPolicy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2), jitterFraction: 0d);

        Assert.Equal(10, backoff.Next().TotalSeconds, 3);
        Assert.Equal(10, backoff.Next().TotalSeconds, 3);
    }

    [Theory]
    [InlineData(0.0, 8.0)]   // random min → base × (1 - f)
    [InlineData(1.0, 12.0)]  // random max → base × (1 + f)
    [InlineData(0.5, 10.0)]  // random mid → base unchanged
    public void Jitter_stays_within_the_configured_bounds(double random, double expectedSeconds)
    {
        // Advance to a base of 10s (1,2,4,8 -> next is 10 capped... use initial 10 for clarity).
        var backoff = new BackoffPolicy(
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), jitterFraction: 0.2d, random: () => random);

        Assert.Equal(expectedSeconds, backoff.Next().TotalSeconds, 3);
    }
}
