using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using RestSharp;

namespace PricingValidationFramework.Tests.System.Ice;

[TestFixture]
public class IceApiClientTests
{
	[TestCase(HttpStatusCode.RequestTimeout)]
	[TestCase(HttpStatusCode.TooManyRequests)]
	[TestCase(HttpStatusCode.InternalServerError)]
	[TestCase(HttpStatusCode.BadGateway)]
	[TestCase(HttpStatusCode.ServiceUnavailable)]
	[TestCase(HttpStatusCode.GatewayTimeout)]
	public async Task GetAsync_should_retry_transient_http_statuses(HttpStatusCode transientStatus)
	{
		var callCount = 0;
		using var client = CreateClient((_, _) =>
		{
			callCount++;
			return Task.FromResult(callCount == 1 ? Response(transientStatus) : Response(HttpStatusCode.OK));
		});

		var payload = await client.GetAsync("https://ice.example.test/quote");

		Assert.Multiple(() =>
		{
			Assert.That(payload, Is.EqualTo("payload"));
			Assert.That(callCount, Is.EqualTo(2));
		});
	}

	[TestCase(HttpStatusCode.BadRequest)]
	[TestCase(HttpStatusCode.Unauthorized)]
	[TestCase(HttpStatusCode.Forbidden)]
	[TestCase(HttpStatusCode.NotFound)]
	[TestCase(HttpStatusCode.Conflict)]
	public void GetAsync_should_not_retry_permanent_http_statuses(HttpStatusCode statusCode)
	{
		var callCount = 0;
		using var client = CreateClient((_, _) =>
		{
			callCount++;
			return Task.FromResult(Response(statusCode));
		});

		var exception = Assert.ThrowsAsync<HttpRequestException>(async () =>
			await client.GetAsync("https://ice.example.test/quote"));

		Assert.Multiple(() =>
		{
			Assert.That(exception!.StatusCode, Is.EqualTo(statusCode));
			Assert.That(callCount, Is.EqualTo(1));
		});
	}

	[Test]
	public async Task GetAsync_should_retry_statusless_network_failures()
	{
		var callCount = 0;
		using var client = CreateClient((_, _) =>
		{
			callCount++;
			if (callCount == 1)
			{
				throw new HttpRequestException("temporary network failure");
			}

			return Task.FromResult(Response(HttpStatusCode.OK));
		});

		var payload = await client.GetAsync("https://ice.example.test/quote");

		Assert.Multiple(() =>
		{
			Assert.That(payload, Is.EqualTo("payload"));
			Assert.That(callCount, Is.EqualTo(2));
		});
	}

	[Test]
	public async Task GetAsync_should_retry_timeout_when_caller_token_is_not_cancelled()
	{
		var callCount = 0;
		using var client = CreateClient((_, cancellationToken) =>
		{
			callCount++;
			if (callCount == 1)
			{
				throw new TaskCanceledException("request timed out", innerException: null, cancellationToken);
			}

			return Task.FromResult(Response(HttpStatusCode.OK));
		});

		var payload = await client.GetAsync("https://ice.example.test/quote");

		Assert.Multiple(() =>
		{
			Assert.That(payload, Is.EqualTo("payload"));
			Assert.That(callCount, Is.EqualTo(2));
		});
	}

	[Test]
	public void GetAsync_should_not_retry_when_caller_cancels()
	{
		using var cancellationTokenSource = new CancellationTokenSource();
		var callCount = 0;
		using var client = CreateClient((_, _) =>
		{
			callCount++;
			cancellationTokenSource.Cancel();
			throw new OperationCanceledException(cancellationTokenSource.Token);
		});

		Assert.That(async () =>
			await client.GetAsync("https://ice.example.test/quote", cancellationTokenSource.Token),
			Throws.InstanceOf<OperationCanceledException>());
		Assert.That(callCount, Is.EqualTo(1));
	}

	private static IceApiClient CreateClient(
		Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseHandler)
	{
		var retrySettings = new RetrySettings
		{
			ApiRetryCount = 2,
			ApiRetryDelaySeconds = 0
		};
		var settings = new IceSettings
		{
			ApiKeyHeaderName = "X-ICE-API-KEY",
			ApiKeyHeaderValue = "test-key"
		};
		var restClient = new RestClient(new HttpClient(new StubHttpMessageHandler(responseHandler)));
		return new IceApiClient(settings, NullLogger<IceApiClient>.Instance, retrySettings, restClient);
	}

	private static HttpResponseMessage Response(HttpStatusCode statusCode)
	{
		return new HttpResponseMessage(statusCode)
		{
			Content = new StringContent("payload")
		};
	}

	private sealed class StubHttpMessageHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseHandler;

		public StubHttpMessageHandler(
			Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseHandler)
		{
			this.responseHandler = responseHandler;
		}

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			return responseHandler(request, cancellationToken);
		}
	}
}
