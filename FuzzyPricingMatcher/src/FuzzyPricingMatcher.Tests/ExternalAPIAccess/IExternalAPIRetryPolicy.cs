namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public interface IExternalAPIRetryPolicy
{
    Task<ExternalAPIResponse> ExecuteAsync(string operationName, Func<int, CancellationToken, Task<ExternalAPIResponse>> attempt, CancellationToken cancellationToken = default);
}