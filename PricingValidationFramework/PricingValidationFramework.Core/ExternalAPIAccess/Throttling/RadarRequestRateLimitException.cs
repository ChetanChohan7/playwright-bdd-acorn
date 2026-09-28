namespace PricingValidationFramework.Core.ExternalAPIAccess.Throttling;

public sealed class RadarRequestRateLimitException : InvalidOperationException
{
	public RadarRequestRateLimitException(string? endpointName, bool queueRejected)
		: base(queueRejected
			? "Radar request rate-limit queue is full."
			: "Radar logical endpoint is missing or unknown.")
	{
		EndpointName = endpointName;
		QueueRejected = queueRejected;
	}

	public string? EndpointName { get; }
	public bool QueueRejected { get; }
}