namespace PricingValidationFramework.Core.ExternalAPIAccess.Throttling;

public interface IRadarRequestRateLimiter // do we need this interface ? 
{
	ValueTask WaitAsync(string endpointName, CancellationToken cancellationToken = default);
}