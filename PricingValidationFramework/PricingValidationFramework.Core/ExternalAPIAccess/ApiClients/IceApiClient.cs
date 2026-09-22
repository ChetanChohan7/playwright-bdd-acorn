namespace PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;

using System.Net.Http.Headers;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using PricingValidationFramework.Core.Configuration;

public class IceApiClient : IDisposable
{
	private readonly HttpClient httpClient;
	private readonly IceSettings settings;
	private readonly RetrySettings retrySettings;
	private readonly ILogger<IceApiClient> logger;

	public IceApiClient(IceSettings settings, ILogger<IceApiClient> logger, RetrySettings? retrySettings = null)
	{
		this.settings = settings;
		this.logger = logger;
		this.retrySettings = retrySettings ?? new RetrySettings();
		httpClient = CreateHttpClient(settings);
	}

	public async Task<string> GetAsync(string url, CancellationToken cancellationToken = default)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Get, url);
				request.Headers.TryAddWithoutValidation(settings.ApiKeyHeaderName, settings.ApiKeyHeaderValue);
				using var response = await httpClient.SendAsync(request, cancellationToken);
				var payload = await response.Content.ReadAsStringAsync(cancellationToken);
				if (!IsTransient(response.StatusCode) || attempt >= retrySettings.ApiRetryCount)
				{
					response.EnsureSuccessStatusCode();
					return payload;
				}

				var retryAfter = response.Headers.RetryAfter?.Delta;
				var delay = retryAfter ?? TimeSpan.FromSeconds(retrySettings.ApiRetryDelaySeconds * Math.Pow(2, attempt));
				logger.LogWarning("Retrying ICE request after HTTP {StatusCode}; attempt {Attempt}.", (int)response.StatusCode, attempt + 1);
				await Task.Delay(delay, cancellationToken);
			}
			catch (HttpRequestException) when (attempt < retrySettings.ApiRetryCount)
			{
				logger.LogWarning("Retrying ICE request after a transient network failure; attempt {Attempt}.", attempt + 1);
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.ApiRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}

	public void Dispose()
	{
		httpClient.Dispose();
	}

	private HttpClient CreateHttpClient(IceSettings iceSettings)
	{
		var handler = new HttpClientHandler();
		var certificate = LoadCertificate(iceSettings);
		handler.ClientCertificates.Add(certificate);

		return new HttpClient(handler, disposeHandler: true);
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

	private static bool IsTransient(HttpStatusCode statusCode)
	{
		return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
			HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or
			HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
	}
}
