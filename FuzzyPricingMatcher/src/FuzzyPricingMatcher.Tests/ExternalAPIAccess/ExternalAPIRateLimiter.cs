namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public interface IApiClock
{
    DateTimeOffset UtcNow { get; }
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class SystemApiClock : IApiClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);
}

public sealed class ExternalAPIRateLimiter : IExternalAPIRateLimiter
{
    private readonly object gate = new();
    private readonly TimeSpan interval;
    private readonly IApiClock clock;
    private DateTimeOffset nextStart;

    public ExternalAPIRateLimiter(int requestsPerSecond, IApiClock? clock = null)
    {
        if (requestsPerSecond <= 0)
            throw new ExternalAPIConfigurationException("API rate limit must be greater than zero.");
        interval = TimeSpan.FromSeconds(1d / requestsPerSecond);
        this.clock = clock ?? new SystemApiClock();
        nextStart = this.clock.UtcNow;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset start;
        lock (gate)
        {
            // Reserve the next process-wide start while holding the lock so callers
            // cannot create separate per-endpoint or per-fixture budgets.
            var now = clock.UtcNow;
            start = nextStart > now ? nextStart : now;
            nextStart = start + interval;
        }
        var delay = start - clock.UtcNow;
        try
        {
            if (delay > TimeSpan.Zero)
                await clock.DelayAsync(delay, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A cancelled waiter did not start a request; return its reservation so
            // cancellation does not consume a future permit.
            lock (gate)
            {
                if (nextStart == start + interval)
                    nextStart = start;
            }
            throw;
        }
    }
}