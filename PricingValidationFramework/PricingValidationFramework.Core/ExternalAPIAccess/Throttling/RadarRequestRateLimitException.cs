namespace PricingValidationFramework.Core.ExternalAPIAccess.Throttling;

public sealed class RadarRequestRateLimitException : InvalidOperationException
{
	public RadarRequestRateLimitException(string? endpointName)
		: base("Radar logical endpoint is missing or unknown.")
	{
		EndpointName = endpointName;
	}

	public string? EndpointName { get; }
}