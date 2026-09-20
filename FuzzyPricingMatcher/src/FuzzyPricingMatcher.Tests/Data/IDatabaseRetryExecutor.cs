namespace FuzzyPricingMatcher.Tests.Data;

public interface IDatabaseRetryExecutor
{
    Task ExecuteAsync(string operationName, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
    Task<T> ExecuteAsync<T>(string operationName, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}