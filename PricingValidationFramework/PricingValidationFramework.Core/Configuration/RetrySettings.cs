namespace PricingValidationFramework.Core.Configuration;

public class RetrySettings
{
	private const double MaxTaskDelaySeconds = (uint.MaxValue - 1d) / 1000d;

	public int DatabaseRetryCount { get; set; } = 3;
	public int DatabaseRetryDelaySeconds { get; set; } = 2;
	public int ApiRetryCount { get; set; } = 3;
	public int ApiRetryDelaySeconds { get; set; } = 2;
	public int ApiRetryAfterMaxDelaySeconds { get; set; } = 60;

	/// Everything the Radar pipeline needs validated up front: its database retries plus its API
	/// retries (which, via ValidateRadarApiRetrySettings, include the generic API check too).
	public void ValidateRadarPipelineSettings()
	{
		ValidateDatabaseRetrySettings();
		ValidateRadarApiRetrySettings();
	}

	public void ValidateDatabaseRetrySettings()
	{
		ValidateRetryValues(
			DatabaseRetryCount,
			DatabaseRetryDelaySeconds,
			nameof(DatabaseRetryCount),
			nameof(DatabaseRetryDelaySeconds));
	}

	public void ValidateApiRetrySettings()
	{
		ValidateRetryValues(
			ApiRetryCount,
			ApiRetryDelaySeconds,
			nameof(ApiRetryCount),
			nameof(ApiRetryDelaySeconds));
	}

	public void ValidateRadarApiRetrySettings()
	{
		ValidateApiRetrySettings();
		if (ApiRetryAfterMaxDelaySeconds < 0 || ApiRetryAfterMaxDelaySeconds > MaxTaskDelaySeconds)
		{
			throw new ArgumentOutOfRangeException(
				nameof(ApiRetryAfterMaxDelaySeconds),
				$"{nameof(ApiRetryAfterMaxDelaySeconds)} must be between zero and {MaxTaskDelaySeconds} seconds.");
		}
	}

	private static void ValidateRetryValues(int retryCount, int delaySeconds, string retryCountName, string delayName)
	{
		if (retryCount < 0)
		{
			throw new ArgumentOutOfRangeException(retryCountName, $"{retryCountName} must be zero or greater.");
		}

		if (delaySeconds < 0)
		{
			throw new ArgumentOutOfRangeException(delayName, $"{delayName} must be zero or greater.");
		}

		if (retryCount == 0 || delaySeconds == 0)
		{
			return;
		}

		var maximumDelaySeconds = delaySeconds * Math.Pow(2, retryCount - 1);
		if (!double.IsFinite(maximumDelaySeconds) || maximumDelaySeconds > MaxTaskDelaySeconds)
		{
			throw new ArgumentOutOfRangeException(
				delayName,
				$"The configured retry count and {delayName} can exceed the supported Task.Delay range of {MaxTaskDelaySeconds} seconds.");
		}
	}
}
