namespace PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess;

public class IceApiClient : IDisposable
{
	private readonly HttpClient httpClient;
	private readonly IceSettings settings;
	private readonly RetrySettings retrySettings;
	private readonly ILogger<IceApiClient> logger;
	private readonly Func<TimeSpan, CancellationToken, Task> retryDelayAsync;

	public IceApiClient(IceSettings settings, ILogger<IceApiClient> logger, RetrySettings? retrySettings = null)
		: this(settings, logger, retrySettings, CreateHttpClient(settings))
	{
	}

	public IceApiClient(
		IceSettings settings,
		ILogger<IceApiClient> logger,
		RetrySettings? retrySettings,
		HttpClient httpClient,
		Func<TimeSpan, CancellationToken, Task>? retryDelayAsync = null)
	{
		this.settings = settings;
		this.logger = logger;
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateApiRetrySettings();
		this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		this.retryDelayAsync = retryDelayAsync ?? Task.Delay;
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
				cancellationToken.ThrowIfCancellationRequested();

				if (!HttpRetryPolicy.IsSuccess(response.StatusCode))
				{
					if (HttpRetryPolicy.IsTransient(response.StatusCode) && attempt < retrySettings.ApiRetryCount)
					{
						var delay = GetLocalRetryDelay(attempt);
						logger.LogWarning("Retrying ICE request after HTTP {StatusCode}; attempt {Attempt}.", (int)response.StatusCode, attempt + 1);
						await retryDelayAsync(delay, cancellationToken);
						continue;
					}

					HttpRetryPolicy.EnsureSuccessStatusCode(response, "Ice");
				}

				return response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync(cancellationToken);
			}
			catch (OperationCanceledException) when (
				!cancellationToken.IsCancellationRequested &&
				attempt < retrySettings.ApiRetryCount)
			{
				logger.LogWarning("Retrying ICE request after a timeout; attempt {Attempt}.", attempt + 1);
				await retryDelayAsync(GetLocalRetryDelay(attempt), cancellationToken);
			}
			catch (HttpRequestException exception) when (exception.StatusCode is null)
			{
				if (attempt >= retrySettings.ApiRetryCount)
				{
					logger.LogError(exception, "ICE request failed after attempt {Attempt}.", attempt + 1);
					throw;
				}

				logger.LogWarning(exception, "Retrying ICE request after a transient network failure; attempt {Attempt}.", attempt + 1);
				await retryDelayAsync(GetLocalRetryDelay(attempt), cancellationToken);
			}
		}
	}

	public void Dispose()
	{
		httpClient.Dispose();
	}

	private TimeSpan GetLocalRetryDelay(int attempt) => HttpRetryPolicy.GetExponentialDelay(attempt, retrySettings.ApiRetryDelaySeconds);

	private static HttpClient CreateHttpClient(IceSettings iceSettings)
	{
	
		var handler = new HttpClientHandler();
		var cert = LoadCertificate(iceSettings);
		Thread.Sleep(2000); // Adding a small delay to ensure the certificate is loaded properly
		handler.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
		Thread.Sleep(2000); 
		handler.ClientCertificates.Add(cert);
        Thread.Sleep(2000); 

		return new HttpClient(handler, disposeHandler: true);
	}

	private static X509Certificate2 LoadCertificate(IceSettings iceSettings)
	{
		X509Certificate2 certificate;

		if (!string.IsNullOrWhiteSpace(iceSettings.PfxCertificateBase64))
		{
			certificate = X509CertificateLoader.LoadPkcs12(
				Convert.FromBase64String(iceSettings.PfxCertificateBase64),
				iceSettings.CertificatePassword);
		}
		else
		{
			certificate = X509CertificateLoader.LoadPkcs12FromFile(
				iceSettings.PfxCertificateFile,
				iceSettings.CertificatePassword);
		}

		try
		{
			using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
			store.Open(OpenFlags.ReadWrite);
			store.Add(certificate);
		}
		catch (Exception)
		{
			// The user certificate store may be unavailable in containers.
		}

		return certificate;
	}
} 