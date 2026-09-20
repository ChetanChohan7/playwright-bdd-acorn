using FuzzyPricingMatcher.Tests.Configuration;
using Microsoft.Data.SqlClient;
using NLog;

namespace FuzzyPricingMatcher.Tests.Data;

public sealed class DatabaseRetryExecutor : IDatabaseRetryExecutor
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ResilienceSettings settings;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;

    public DatabaseRetryExecutor(ResilienceSettings settings, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.settings = settings;
        this.delay = delay ?? Task.Delay;
    }

    public Task ExecuteAsync(string operationName, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) => ExecuteAsync<object?>(operationName, async token => { await operation(token); return null; }, cancellationToken);

    public async Task<T> ExecuteAsync<T>(string operationName, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var attempts = Math.Max(1, settings.SqlRetryAttempts);
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return await operation(cancellationToken); }
            catch (Exception exception) when (attempt < attempts && IsTransient(exception) && !cancellationToken.IsCancellationRequested)
            {
                var exponential = Math.Min(settings.SqlRetryMaximumDelaySeconds, settings.SqlRetryInitialDelaySeconds * Math.Pow(2, attempt - 1));
                var jitter = Random.Shared.NextDouble() * Math.Max(0.01, exponential * 0.2);
                var wait = TimeSpan.FromSeconds(Math.Min(settings.SqlRetryMaximumDelaySeconds, exponential + jitter));
                Logger.Warn("Retrying database operation {OperationName}; attempt {Attempt}; delay {DelayMilliseconds}ms; exceptionType={ExceptionType}", operationName, attempt, wait.TotalMilliseconds, exception.GetType().Name);
                await delay(wait, cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception exception) => exception is TimeoutException || exception is SqlException sqlException && sqlException.Errors.Cast<SqlError>().Any(error => error.Number is 2 or 53 or 1205 or -2 or 4060 or 40197 or 40501 or 49918 or 49919 or 49920);
}