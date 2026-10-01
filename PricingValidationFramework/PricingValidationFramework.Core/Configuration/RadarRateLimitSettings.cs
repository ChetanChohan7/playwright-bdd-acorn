namespace PricingValidationFramework.Core.Configuration;

public sealed class RadarRateLimitSettings
{
	public int RequestsPerSecond { get; init; } = 2;
	public int QueueLimit { get; init; } = 4;

	/// Defaults above are only what's used if config doesn't set these - Validate() still has to
	/// run after binding, since config can override them with zero or a negative value.
	public void Validate()
	{
		if (RequestsPerSecond <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(RequestsPerSecond), "RequestsPerSecond must be greater than zero.");
		}

		if (QueueLimit <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(QueueLimit), "QueueLimit must be greater than zero.");
		}
	}
}
