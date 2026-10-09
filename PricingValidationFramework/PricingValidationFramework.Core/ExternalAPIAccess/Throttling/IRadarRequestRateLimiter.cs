namespace PricingValidationFramework.Core.ExternalAPIAccess.Throttling;

public interface IRadarRequestRateLimiter
{
	Task<HttpResponseMessage> SendAsync(
		string endpointName,
		Func<Task<HttpResponseMessage>> sendAsync,
		CancellationToken cancellationToken = default);
}