namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public interface IExternalAPIRateLimiter
{
    Task WaitAsync(CancellationToken cancellationToken = default);
}