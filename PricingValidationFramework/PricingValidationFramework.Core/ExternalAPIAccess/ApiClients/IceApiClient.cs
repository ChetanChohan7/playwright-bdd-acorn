namespace PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;

using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using PricingValidationFramework.Core.Configuration;
using RestSharp;

public class IceApiClient : IDisposable
{
	private readonly RestClient restClient;
	private readonly IceSettings settings;
	private readonly RetrySettings retrySettings;
	private readonly ILogger<IceApiClient> logger;
	private readonly Func<TimeSpan, CancellationToken, Task> retryDelayAsync;

	public IceApiClient(IceSettings settings, ILogger<IceApiClient> logger, RetrySettings? retrySettings = null)
		: this(settings, logger, retrySettings, CreateRestClient(settings))
	{
	}

	public IceApiClient(
		IceSettings settings,
		ILogger<IceApiClient> logger,
		RetrySettings? retrySettings,
		RestClient restClient,
		Func<TimeSpan, CancellationToken, Task>? retryDelayAsync = null)
	{
		this.settings = settings;
		this.logger = logger;
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateIceApiRetrySettings();
		this.restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
		this.retryDelayAsync = retryDelayAsync ?? Task.Delay;
	}

	public async Task<string> GetAsync(string url, CancellationToken cancellationToken = default)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				var request = new RestRequest(url, Method.Get)
					.AddOrUpdateHeader(settings.ApiKeyHeaderName, settings.ApiKeyHeaderValue);
				var response = await restClient.ExecuteAsync(request, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();

				if (response.StatusCode != 0 && !IsSuccess(response.StatusCode))
				{
					if (IsTransient(response.StatusCode) && attempt < retrySettings.ApiRetryCount)
					{
						var delay = GetLocalRetryDelay(attempt);
						logger.LogWarning("Retrying ICE request after HTTP {StatusCode}; attempt {Attempt}.", (int)response.StatusCode, attempt + 1);
						await retryDelayAsync(delay, cancellationToken);
						continue;
					}

					EnsureSuccessStatusCode(response);
				}

				if (response.ResponseStatus != ResponseStatus.Completed)
				{
					throw new HttpRequestException(response.ErrorMessage, response.ErrorException);
				}

				return response.Content ?? string.Empty;
			}
			catch (OperationCanceledException) when (
				!cancellationToken.IsCancellationRequested &&
				attempt < retrySettings.ApiRetryCount)
			{
				logger.LogWarning("Retrying ICE request after a timeout; attempt {Attempt}.", attempt + 1);
				await retryDelayAsync(GetLocalRetryDelay(attempt), cancellationToken);
			}
			catch (HttpRequestException exception) when (
				attempt < retrySettings.ApiRetryCount &&
				exception.StatusCode is null)
			{
				logger.LogWarning("Retrying ICE request after a transient network failure; attempt {Attempt}.", attempt + 1);
				await retryDelayAsync(GetLocalRetryDelay(attempt), cancellationToken);
			}
		}
	}

	public void Dispose()
	{
		restClient.Dispose();
	}

	private TimeSpan GetLocalRetryDelay(int attempt)
	{
		return TimeSpan.FromSeconds(retrySettings.ApiRetryDelaySeconds * Math.Pow(2, attempt));
	}

	private static RestClient CreateRestClient(IceSettings iceSettings) 
	{
		var options = new RestClientOptions
		{
			ClientCertificates = [LoadCertificate(iceSettings)]
		};

		return new RestClient(options);
	}

	private static X509Certificate2 LoadCertificate(IceSettings iceSettings)
	{
		if (!string.IsNullOrWhiteSpace(iceSettings.PfxCertificateBase64))
		{
			return X509CertificateLoader.LoadPkcs12(
				Convert.FromBase64String(iceSettings.PfxCertificateBase64),
				iceSettings.CertificatePassword,
				X509KeyStorageFlags.EphemeralKeySet);
		}

		return X509CertificateLoader.LoadPkcs12FromFile(
			iceSettings.PfxCertificateFile,
			iceSettings.CertificatePassword,
			X509KeyStorageFlags.EphemeralKeySet);
	}

	private static TimeSpan? GetRetryAfter(RestResponse response) // same for this IS THIS COMMON IN BOTH RADARA AND  icd 
	{
		var value = response.Headers?
			.FirstOrDefault(header => string.Equals(header.Name, "Retry-After", StringComparison.OrdinalIgnoreCase))
			?.Value?.ToString();

		return int.TryParse(value, out var seconds) ? TimeSpan.FromSeconds(seconds) : null;
	}

	private static void EnsureSuccessStatusCode(RestResponse response) // this to ensure the response indicates a successful HTTP status code
	{
		if ((int)response.StatusCode is >= 200 and <= 299)
		{
			return;
		}

		throw new HttpRequestException(
			$"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusDescription}).",
			response.ErrorException,
			response.StatusCode);
	}

	private static bool IsSuccess(HttpStatusCode statusCode) // this as well utils class for HTTP status codes
	{
		return (int)statusCode is >= 200 and <= 299;
	}

	private static bool IsTransient(HttpStatusCode statusCode) // can we not have this in a common utility class for HTTP status codes?
	{
		return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
			HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or
			HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
	}
}
// refactor class 