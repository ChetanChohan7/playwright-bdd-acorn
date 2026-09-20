namespace FuzzyPricingMatcher.Tests.Api;

public interface IApiRateLimiter
{
    Task WaitAsync(CancellationToken cancellationToken = default);
}