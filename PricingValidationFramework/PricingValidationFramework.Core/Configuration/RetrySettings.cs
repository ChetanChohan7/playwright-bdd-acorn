namespace PricingValidationFramework.Core.Configuration;

public class RetrySettings
{
	public int DatabaseRetryCount { get; set; } = 3;
	public int DatabaseRetryDelaySeconds { get; set; } = 2;
	public int ApiRetryCount { get; set; } = 3;
	public int ApiRetryDelaySeconds { get; set; } = 2;
}
