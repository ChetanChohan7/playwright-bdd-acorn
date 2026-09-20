namespace FuzzyPricingMatcher.Tests.Api;

public interface IApiRetryPipeline
{
    Task<ApiCallResult> ExecuteAsync(string operationName, Func<int, CancellationToken, Task<ApiCallResult>> attempt, CancellationToken cancellationToken = default);
}