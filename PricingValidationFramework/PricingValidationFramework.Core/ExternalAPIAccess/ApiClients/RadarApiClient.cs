namespace PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;

using System.Net;
using System.Globalization;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using RestSharp;

public class RadarApiClient : IDisposable
{
	private readonly RestClient restClient;
	private readonly RetrySettings retrySettings;
	private readonly ILogger<RadarApiClient> logger;
	private readonly IRadarRequestRateLimiter rateLimiter;
	private readonly Func<TimeSpan, CancellationToken, Task> retryDelayAsync;

	public RadarApiClient(
		IRadarRequestRateLimiter rateLimiter,
		RestClient? restClient = null,
		ILogger<RadarApiClient>? logger = null,
		RetrySettings? retrySettings = null,
		Func<TimeSpan, CancellationToken, Task>? retryDelayAsync = null)
	{
		this.rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
		this.restClient = restClient ?? new RestClient();
		this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RadarApiClient>.Instance;
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateRadarApiRetrySettings();
		this.retryDelayAsync = retryDelayAsync ?? Task.Delay;
	}

	public async Task<string> PostAsync(
		string endpointName,
		string url,
		string apiKeyHeaderName,
		string apiKeyValue,
		string requestXml,
		CancellationToken cancellationToken = default,
		string? scenarioId = null,
		string? quoteRef = null,
		string? productCode = null,
		string? schemeCode = null)
	{
		ValidateRequestInputs(url, apiKeyHeaderName, apiKeyValue, requestXml);

		cancellationToken.ThrowIfCancellationRequested();
		var diagnostics = new RequestDiagnostics(endpointName, scenarioId, quoteRef, productCode, schemeCode);

		for (var attempt = 0; ; attempt++)
		{
			try
			{
				var response = await ExecuteAttemptAsync(
					url,
					apiKeyHeaderName,
					apiKeyValue,
					requestXml,
					diagnostics,
					attempt,
					cancellationToken);

				if (response.StatusCode != 0 && !IsSuccess(response.StatusCode))
				{
					if (IsTransient(response.StatusCode) && attempt < retrySettings.ApiRetryCount)
					{
						await DelayBeforeRetryAsync(
							response,
							attempt,
							cancellationToken,
							diagnostics);
						continue;
					}

					EnsureSuccessStatusCode(response);
				}

				if (response.ResponseStatus != ResponseStatus.Completed)
				{
					throw new HttpRequestException(response.ErrorMessage ?? "Radar request did not complete.", response.ErrorException);
				}

				if (string.IsNullOrWhiteSpace(response.Content))
				{
					throw new InvalidOperationException("Radar response content is empty.");
				}

				return response.Content;
			}
			catch (OperationCanceledException)
				when (!cancellationToken.IsCancellationRequested && attempt < retrySettings.ApiRetryCount)
			{
				var delay = GetLocalRetryDelay(attempt);
				logger.LogWarning(
					"Retrying Radar request after a request timeout. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}, ProductCode={ProductCode}, SchemeCode={SchemeCode}, EndpointName={EndpointName}, Attempt={Attempt}, MaxAttempts={MaxAttempts}, RetryDelay={RetryDelay}, ExecutionStage={ExecutionStage}.",
				diagnostics.ScenarioId,
				diagnostics.QuoteRef,
				diagnostics.ProductCode,
				diagnostics.SchemeCode,
				diagnostics.EndpointName,
					attempt + 1,
					retrySettings.ApiRetryCount,
					delay,
					"RetryDelay");
				await retryDelayAsync(delay, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (HttpRequestException ex) when (attempt < retrySettings.ApiRetryCount && ex.StatusCode is null)
			{
				var delay = GetLocalRetryDelay(attempt);
				logger.LogWarning(
					"Retrying Radar request after a transient network failure. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}, ProductCode={ProductCode}, SchemeCode={SchemeCode}, EndpointName={EndpointName}, Attempt={Attempt}, MaxAttempts={MaxAttempts}, RetryDelay={RetryDelay}, RetryAfterUsed={RetryAfterUsed}, ExecutionStage={ExecutionStage}.",
					diagnostics.ScenarioId,
					diagnostics.QuoteRef,
					diagnostics.ProductCode,
					diagnostics.SchemeCode,
					diagnostics.EndpointName,
					attempt + 1,
					retrySettings.ApiRetryCount,
					delay,
					false,
					"RetryDelay");
				await retryDelayAsync(delay, cancellationToken);
			}
		}
	}

	private static void ValidateRequestInputs(string url, string apiKeyHeaderName, string apiKeyValue, string requestXml)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			throw new ArgumentException("Radar request URL is required.", nameof(url));
		}

		if (string.IsNullOrWhiteSpace(apiKeyHeaderName))
		{
			throw new ArgumentException("Radar API key header name is required.", nameof(apiKeyHeaderName));
		}

		if (string.IsNullOrWhiteSpace(apiKeyValue))
		{
			throw new ArgumentException("Radar API key value is required.", nameof(apiKeyValue));
		}

		if (string.IsNullOrWhiteSpace(requestXml))
		{
			throw new ArgumentException("Radar request XML is required.", nameof(requestXml));
		}
	}

	private async Task<RestResponse> ExecuteAttemptAsync(
		string url,
		string apiKeyHeaderName,
		string apiKeyValue,
		string requestXml,
		RequestDiagnostics diagnostics,
		int attempt,
		CancellationToken cancellationToken)
	{
		var permitWaitStart = Stopwatch.GetTimestamp();
		await rateLimiter.WaitAsync(diagnostics.EndpointName, cancellationToken);

		logger.LogDebug(
			"Radar request permit acquired. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}, ProductCode={ProductCode}, SchemeCode={SchemeCode}, EndpointName={EndpointName}, Attempt={Attempt}, ExecutionStage={ExecutionStage}, PermitWaitDuration={PermitWaitDuration}.",
			diagnostics.ScenarioId,
			diagnostics.QuoteRef,
			diagnostics.ProductCode,
			diagnostics.SchemeCode,
			diagnostics.EndpointName,
			attempt + 1,
			"RateLimitPermit",
			Stopwatch.GetElapsedTime(permitWaitStart));

		var request = new RestRequest(url, Method.Post)
			.AddHeader(apiKeyHeaderName, apiKeyValue)
			.AddHeader("Accept", "application/xml")
			.AddStringBody(requestXml, ContentType.Xml);

		var response = await restClient.ExecuteAsync(request, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		return response;
	}

	public void Dispose()
	{
		restClient.Dispose();
	}

	private async Task DelayBeforeRetryAsync(
		RestResponse response,
		int attempt,
		CancellationToken cancellationToken,
		RequestDiagnostics diagnostics)
	{
		var delay = GetRetryDelay(
			response,
			attempt,
			out var retryAfterUsed,
			diagnostics);
		logger.LogWarning(
			"Retrying Radar request after HTTP {StatusCode}. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}, ProductCode={ProductCode}, SchemeCode={SchemeCode}, EndpointName={EndpointName}, Attempt={Attempt}, MaxAttempts={MaxAttempts}, RetryDelay={RetryDelay}, RetryAfterUsed={RetryAfterUsed}, ExecutionStage={ExecutionStage}.",
			(int)response.StatusCode,
			diagnostics.ScenarioId,
			diagnostics.QuoteRef,
			diagnostics.ProductCode,
			diagnostics.SchemeCode,
			diagnostics.EndpointName,
			attempt + 1,
			retrySettings.ApiRetryCount,
			delay,
			retryAfterUsed,
			"RetryDelay");
		await retryDelayAsync(delay, cancellationToken);
	}

	private TimeSpan GetLocalRetryDelay(int attempt)
	{
		return TimeSpan.FromSeconds(retrySettings.ApiRetryDelaySeconds * Math.Pow(2, attempt));
	}

	private TimeSpan GetRetryDelay(
		RestResponse response,
		int attempt,
		out bool retryAfterUsed,
		RequestDiagnostics diagnostics)
	{
		var localDelay = GetLocalRetryDelay(attempt);
		var retryAfterValue = response.Headers?
			.FirstOrDefault(header => string.Equals(header.Name, "Retry-After", StringComparison.OrdinalIgnoreCase))
			?.Value?.ToString();
		var retryAfterDelay = ParseRetryAfterDelay(retryAfterValue);
		var maximumRetryAfterDelay = TimeSpan.FromSeconds(retrySettings.ApiRetryAfterMaxDelaySeconds);
		var wasCapped = retryAfterDelay > maximumRetryAfterDelay;
		if (wasCapped)
		{
			retryAfterDelay = maximumRetryAfterDelay;
		}

		retryAfterUsed = retryAfterDelay is { } parsedDelay && parsedDelay > localDelay;
		var effectiveDelay = retryAfterDelay is { } serverDelay && serverDelay > localDelay
			? serverDelay
			: localDelay;

		if (wasCapped)
		{
			logger.LogWarning(
				"Radar Retry-After was capped. EndpointName={EndpointName}, Attempt={Attempt}, SuppliedRetryAfter={SuppliedRetryAfter}, ConfiguredMaximum={ConfiguredMaximum}, EffectiveDelay={EffectiveDelay}.",
				diagnostics.EndpointName,
				attempt + 1,
				retryAfterValue,
				maximumRetryAfterDelay,
				effectiveDelay);
		}

		if (retryAfterValue is not null && !retryAfterDelay.HasValue)
		{
			logger.LogWarning(
				"Radar response contained an invalid Retry-After value. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}, ProductCode={ProductCode}, SchemeCode={SchemeCode}, EndpointName={EndpointName}, Attempt={Attempt}, ExecutionStage={ExecutionStage}.",
				diagnostics.ScenarioId,
				diagnostics.QuoteRef,
				diagnostics.ProductCode,
				diagnostics.SchemeCode,
				diagnostics.EndpointName,
				attempt + 1,
				"RetryAfterParsing");
		}

		return effectiveDelay;
	}

	private static TimeSpan? ParseRetryAfterDelay(string? retryAfterValue)
	{
		if (string.IsNullOrWhiteSpace(retryAfterValue))
		{
			return null;
		}

		if (int.TryParse(retryAfterValue, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
		{
			return TimeSpan.FromSeconds(seconds);
		}

		var httpDateFormats = new[]
		{
			"r",
			"dddd, dd-MMM-yy HH:mm:ss 'GMT'",
			"ddd MMM d HH:mm:ss yyyy"
		};
		if (DateTimeOffset.TryParseExact(
			retryAfterValue,
			httpDateFormats,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
			out var retryAfterDate))
		{
			var delay = retryAfterDate - DateTimeOffset.UtcNow;
			return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
		}

		return null;
	}

	private static bool IsTransient(HttpStatusCode statusCode)  // same as ice apiclient 
	{
		return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
			HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or
			HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
	}

	private static bool IsSuccess(HttpStatusCode statusCode) // same as ice api client
	{
		return (int)statusCode is >= 200 and <= 299;
	}

	private static void EnsureSuccessStatusCode(RestResponse response) //same as ice api client 
	{
		if (IsSuccess(response.StatusCode))
		{
			return;
		}

		throw new HttpRequestException(
			$"Radar request failed with status {(int)response.StatusCode} ({response.StatusDescription}).",
			response.ErrorException,
			response.StatusCode);
	}

	private readonly record struct RequestDiagnostics(
		string EndpointName,
		string? ScenarioId,
		string? QuoteRef,
		string? ProductCode,
		string? SchemeCode);
}
