namespace PricingValidationFramework.Core.ExternalAPIAccess.Throttling;

using System.Threading.RateLimiting;
using PricingValidationFramework.Core.Configuration;

public sealed class RadarRequestRateLimiter : IRadarRequestRateLimiter, IDisposable
{
	private readonly Dictionary<string, SlidingWindowRateLimiter> endpointLimiters;

	public RadarRequestRateLimiter(RadarRateLimitSettings settings, IEnumerable<string> endpointNames)
	{
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(endpointNames);
		endpointLimiters = new Dictionary<string, SlidingWindowRateLimiter>(StringComparer.OrdinalIgnoreCase);
		foreach (var endpointName in endpointNames)
		{
			var limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
			{
				PermitLimit = settings.RequestsPerSecond,
				Window = TimeSpan.FromSeconds(1),
				SegmentsPerWindow = 10,
				QueueLimit = settings.QueueLimit,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				AutoReplenishment = true
			});
			if (endpointLimiters.TryAdd(endpointName, limiter))
			{
				continue;
			}

			limiter.Dispose();
			Dispose();
			throw new ArgumentException("Radar logical endpoint names must be unique ignoring case.", nameof(endpointNames));
		}
	}

	public async ValueTask WaitAsync(string endpointName, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(endpointName) || !endpointLimiters.TryGetValue(endpointName, out var limiter))
		{
			throw new RadarRequestRateLimitException(null, queueRejected: false);
		}

		using var lease = await limiter.AcquireAsync(1, cancellationToken);
		if (!lease.IsAcquired)
		{
			var configuredEndpointName = endpointLimiters.Keys.First(
				configuredName => string.Equals(configuredName, endpointName, StringComparison.OrdinalIgnoreCase));
			throw new RadarRequestRateLimitException(configuredEndpointName, queueRejected: true);
		}
	}

	public void Dispose()
	{
		foreach (var limiter in endpointLimiters.Values)
		{
			limiter.Dispose();
		}
	}
}