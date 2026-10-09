namespace PricingValidationFramework.Core.ExternalAPIAccess.Throttling;

using System.Diagnostics;
using PricingValidationFramework.Core.Configuration;

public sealed class RadarRequestRateLimiter : IRadarRequestRateLimiter, IDisposable
{
	private readonly HashSet<string> endpointNames;
	private readonly SemaphoreSlim dispatchGate;
	private readonly TimeSpan requestInterval;
	private long lastDispatchTimestamp;
	private bool hasDispatched;

	public RadarRequestRateLimiter(RadarRateLimitSettings settings, IEnumerable<string> endpointNames)
	{
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(endpointNames);
		settings.Validate();
		this.endpointNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var endpointName in endpointNames)
		{
			if (!this.endpointNames.Add(endpointName))
			{
				throw new ArgumentException("Radar logical endpoint names must be unique ignoring case.", nameof(endpointNames));
			}
		}

		requestInterval = TimeSpan.FromTicks((long)Math.Ceiling((double)TimeSpan.TicksPerSecond / settings.RequestsPerSecond));
		dispatchGate = new SemaphoreSlim(1, 1);
	}

	public async Task<HttpResponseMessage> SendAsync(
		string endpointName,
		Func<Task<HttpResponseMessage>> sendAsync,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(sendAsync);
		if (string.IsNullOrWhiteSpace(endpointName) || !endpointNames.Contains(endpointName))
		{
			throw new RadarRequestRateLimitException(endpointName);
		}

		Task<HttpResponseMessage> responseTask;
		await dispatchGate.WaitAsync(cancellationToken);
		try
		{
			while (hasDispatched)
			{
				var remainingDelay = requestInterval - Stopwatch.GetElapsedTime(lastDispatchTimestamp);
				if (remainingDelay <= TimeSpan.Zero)
				{
					break;
				}

				await Task.Delay(remainingDelay < TimeSpan.FromMilliseconds(1)
					? TimeSpan.FromMilliseconds(1)
					: remainingDelay, cancellationToken);
			}

			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				responseTask = sendAsync();
			}
			finally
			{
				lastDispatchTimestamp = Stopwatch.GetTimestamp();
				hasDispatched = true;
			}
		}
		finally
		{
			dispatchGate.Release();
		}

		return await responseTask;
	}

	public void Dispose()
	{
		dispatchGate.Dispose();
	}
}