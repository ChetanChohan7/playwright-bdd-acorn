namespace PricingValidationFramework.Core.ExternalAPIAccess.Throttling;

public interface IRadarRequestRateLimiter
{
	ValueTask WaitAsync(string endpointName, CancellationToken cancellationToken = default);
}