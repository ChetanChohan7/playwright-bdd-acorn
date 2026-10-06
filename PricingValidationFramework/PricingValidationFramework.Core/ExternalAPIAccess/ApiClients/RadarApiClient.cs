namespace PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;

using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Core.Models.External;

public class RadarApiClient : IDisposable
{
	private readonly HttpClient httpClient;
	private readonly RetrySettings retrySettings;
	private readonly ILogger<RadarApiClient> logger;
	private readonly IRadarRequestRateLimiter rateLimiter;
	private readonly Func<TimeSpan, CancellationToken, Task> retryDelayAsync;

	public RadarApiClient(
		IRadarRequestRateLimiter rateLimiter,
		HttpClient? httpClient = null,
		ILogger<RadarApiClient>? logger = null,
		RetrySettings? retrySettings = null,
		Func<TimeSpan, CancellationToken, Task>? retryDelayAsync = null)
	{
		this.rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
		this.httpClient = httpClient ?? new HttpClient();
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
				using var response = await ExecuteAttemptAsync(
					url,
					apiKeyHeaderName,
					apiKeyValue,
					requestXml,
					diagnostics,
					attempt,
					cancellationToken);

				if (!HttpRetryPolicy.IsSuccess(response.StatusCode))
				{
					if (HttpRetryPolicy.IsTransient(response.StatusCode) && attempt < retrySettings.ApiRetryCount)
					{
						await DelayBeforeRetryAsync(
							response,
							attempt,
							cancellationToken,
							diagnostics);
						continue;
					}

					HttpRetryPolicy.EnsureSuccessStatusCode(response, "Radar");
				}

				var content = response.Content is null ? null : await response.Content.ReadAsStringAsync(cancellationToken);
				if (string.IsNullOrWhiteSpace(content))
				{
					throw new InvalidOperationException("Radar response content is empty.");
				}

				return NormalizeResponseXml(content);
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

	private async Task<HttpResponseMessage> ExecuteAttemptAsync(
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

		using var request = new HttpRequestMessage(HttpMethod.Post, url);
		request.Headers.TryAddWithoutValidation(apiKeyHeaderName, apiKeyValue);
		request.Headers.Accept.ParseAdd("application/json");
		request.Content = new StringContent(requestXml, Encoding.UTF8, "application/xml");

		var response = await httpClient.SendAsync(request, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		return response;
	}

	private static string NormalizeResponseXml(string content)
	{
		var responseContent = content.TrimStart('\uFEFF').Trim();
		string xml;

		if (responseContent.StartsWith('<'))
		{
			xml = responseContent;
		}
		else
		{
			var envelope = JsonSerializer.Deserialize<RadarJsonResponse>(
				responseContent,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			xml = envelope?.Response?.TrimStart('\uFEFF').Trim()
				?? throw new InvalidDataException("Radar JSON response does not contain an XML response value.");
		}

		var document = XDocument.Parse(xml);
		return document.Root?.ToString(SaveOptions.DisableFormatting)
			?? throw new InvalidDataException("Radar response XML does not contain a root element.");
	}

	public void Dispose()
	{
		httpClient.Dispose();
	}

	private async Task DelayBeforeRetryAsync(
		HttpResponseMessage response,
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

	private TimeSpan GetLocalRetryDelay(int attempt) => HttpRetryPolicy.GetExponentialDelay(attempt, retrySettings.ApiRetryDelaySeconds);

	private TimeSpan GetRetryDelay(
		HttpResponseMessage response,
		int attempt,
		out bool retryAfterUsed,
		RequestDiagnostics diagnostics)
	{
		var localDelay = GetLocalRetryDelay(attempt);
		var retryAfterValue = response.Headers.TryGetValues("Retry-After", out var values)
			? values.FirstOrDefault()
			: null;
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

	private readonly record struct RequestDiagnostics(
		string EndpointName,
		string? ScenarioId,
		string? QuoteRef,
		string? ProductCode,
		string? SchemeCode);
}
