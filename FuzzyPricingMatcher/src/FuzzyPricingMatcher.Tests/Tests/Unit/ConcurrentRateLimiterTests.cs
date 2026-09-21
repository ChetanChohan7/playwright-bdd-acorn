using FuzzyPricingMatcher.Tests.ExternalAPIAccess;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

[TestFixture]
[Category("Unit")]
public sealed class ConcurrentRateLimiterTests
{
    [Test]
    public async Task Concurrent_callers_share_one_global_schedule()
    {
        var clock = new ControlledClock();
        var limiter = new ExternalAPIRateLimiter(2, clock);
        var waits = Enumerable.Range(0, 4).Select(_ => limiter.WaitAsync()).ToArray();

        await Task.WhenAll(waits);

        Assert.That(clock.Delays, Is.EqualTo(new[]
        {
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromMilliseconds(500)
        }));
    }

    [Test]
    public async Task Cancelled_waiter_does_not_corrupt_the_next_permit()
    {
        var clock = new CancellationClock();
        var limiter = new ExternalAPIRateLimiter(2, clock);
        await limiter.WaitAsync();
        using var cancellation = new CancellationTokenSource();
        var cancelled = limiter.WaitAsync(cancellation.Token);
        cancellation.Cancel();

        Assert.That(async () => await cancelled, Throws.InstanceOf<OperationCanceledException>());
        clock.AllowDelays = true;
        await limiter.WaitAsync();

        Assert.That(clock.Delays.Last(), Is.EqualTo(TimeSpan.FromMilliseconds(500)));
    }

    [Test]
    public async Task Two_starts_per_second_schedule_starts_about_500_milliseconds_apart_without_sleeping()
    {
        var clock = new ControlledClock();
        var limiter = new ExternalAPIRateLimiter(2, clock);
        await limiter.WaitAsync();
        await limiter.WaitAsync();

        Assert.That(clock.Delays.Single(), Is.EqualTo(TimeSpan.FromMilliseconds(500)));
    }

    private sealed class ControlledClock : IApiClock
    {
        public DateTimeOffset Current { get; private set; } = DateTimeOffset.UnixEpoch;
        public List<TimeSpan> Delays { get; } = [];
        public DateTimeOffset UtcNow => Current;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            Current += delay;
            return Task.CompletedTask;
        }
    }

    private sealed class CancellationClock : IApiClock
    {
        public DateTimeOffset Current { get; private set; } = DateTimeOffset.UnixEpoch;
        public List<TimeSpan> Delays { get; } = [];
        public bool AllowDelays { get; set; }
        public DateTimeOffset UtcNow => Current;
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            if (AllowDelays)
            {
                Current += delay;
                return Task.CompletedTask;
            }
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
