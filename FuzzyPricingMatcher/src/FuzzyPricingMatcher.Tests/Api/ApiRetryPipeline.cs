using FuzzyPricingMatcher.Tests.Configuration;
using NLog;

namespace FuzzyPricingMatcher.Tests.Api;

public sealed class ApiRetryPipeline : IApiRetryPipeline
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ResilienceSettings settings;
    private readonly IApiRateLimiter rateLimiter;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;

    public ApiRetryPipeline(ResilienceSettings settings, IApiRateLimiter rateLimiter, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.settings = settings;
        this.rateLimiter = rateLimiter;
        this.delay = delay ?? Task.Delay;
    }

    public async Task<ApiCallResult> ExecuteAsync(string operationName, Func<int, CancellationToken, Task<ApiCallResult>> attempt, CancellationToken cancellationToken = default)
    {
        var maximumAttempts = Math.Max(1, settings.ApiRetryAttempts + 1);
        for (var physicalAttempt = 1; physicalAttempt <= maximumAttempts; physicalAttempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await rateLimiter.WaitAsync(cancellationToken);
            ApiCallResult result;
            try { result = await attempt(physicalAttempt, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) when (physicalAttempt < maximumAttempts && IsTransient(exception))
            {
                await WaitBeforeRetry(operationName, physicalAttempt, maximumAttempts, null, cancellationToken);
                continue;
            }
            if (result.Successful || !ShouldRetry(result.StatusCode) || physicalAttempt == maximumAttempts)
                return result;
            await WaitBeforeRetry(operationName, physicalAttempt, maximumAttempts, result.RetryAfter, cancellationToken);
        }
        throw new ApiResponseException("API retry pipeline exhausted without a result.");
    }

    private async Task WaitBeforeRetry(string operationName, int attempt, int maximumAttempts, TimeSpan? retryAfter, CancellationToken cancellationToken)
    {
        var exponential = Math.Min(settings.ApiRetryMaximumDelaySeconds, settings.ApiRetryInitialDelaySeconds * Math.Pow(2, attempt - 1));
        var jitter = Random.Shared.NextDouble() * Math.Max(0.01, exponential * 0.2);
        var delaySeconds = Math.Max(retryAfter?.TotalSeconds ?? 0, Math.Min(settings.ApiRetryMaximumDelaySeconds, exponential + jitter));
        var wait = TimeSpan.FromSeconds(delaySeconds);
        Logger.Info("API retry {OperationName}; attempt {Attempt}; maximum attempts {MaximumAttempts}; delay {DelayMilliseconds}ms", operationName, attempt, maximumAttempts, wait.TotalMilliseconds);
        await delay(wait, cancellationToken);
    }

    private static bool IsTransient(Exception exception) => exception is ApiTransportException or HttpRequestException or TimeoutException or TaskCanceledException;

    private static bool ShouldRetry(int? statusCode) => statusCode is 408 or 429 or 502 or 503 or 504;
}