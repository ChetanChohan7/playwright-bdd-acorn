namespace PricingValidationFramework.Core.Configuration;

public class RetrySettings
{
	public int DatabaseRetryCount { get; set; } = 3;
	public int DatabaseRetryDelaySeconds { get; set; } = 2;
	public int ApiRetryCount { get; set; } = 3;
	public int ApiRetryDelaySeconds { get; set; } = 2;
	public int ApiRetryAfterMaxDelaySeconds { get; set; } = 60;

	public void Validate() // what are you validating here? change hte name of the method 
	{
		if (ApiRetryAfterMaxDelaySeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(ApiRetryAfterMaxDelaySeconds),
				"ApiRetryAfterMaxDelaySeconds must be greater than zero.");
		}
	}
}
