namespace FuzzyPricingMatcher.Tests.Configuration;

public sealed class ResilienceSettings
{
    public int ApiRetryAttempts { get; set; } = 3;
    public int SqlRetryAttempts { get; set; } = 3;
    public int ApiRetryInitialDelaySeconds { get; set; } = 1;
    public int ApiRetryMaximumDelaySeconds { get; set; } = 10;
    public int SqlRetryInitialDelaySeconds { get; set; } = 1;
    public int SqlRetryMaximumDelaySeconds { get; set; } = 8;
    public int ApiTimeoutSeconds { get; set; } = 60;
    public int SqlCommandTimeoutSeconds { get; set; } = 60;
    public int ApiRateLimitPerSecond { get; set; } = 2;
    public int ParallelWorkers { get; set; } = 4;
}