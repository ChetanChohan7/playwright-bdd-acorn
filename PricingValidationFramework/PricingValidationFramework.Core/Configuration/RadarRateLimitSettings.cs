namespace PricingValidationFramework.Core.Configuration;

public sealed class RadarRateLimitSettings
{     // we specifiying this here to give it a default value why are we checking if its o 
	public int RequestsPerSecond { get; init; } = 2;
	public int QueueLimit { get; init; } = 4;

	public void Validate() // what are you validating here why if you are specifying a default > 0
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
