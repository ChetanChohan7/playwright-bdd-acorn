using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;

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

	[TestCase(null)]
	[TestCase("30")]
	[TestCase("0")]
	[TestCase("-5")]
	[TestCase("invalid")]
	[TestCase("Wed, 21 Oct 2030 07:28:00 GMT")]
	[TestCase("999999999999999999999999999999999999")]
	[TestCase(" ")]
	[TestCase("malformed value")]
	public async Task GetAsync_should_use_local_backoff_regardless_of_retry_after_header(string? retryAfter)
	{
		var callCount = 0;
		var delays = new List<TimeSpan>();
		using var client = CreateClient((_, _) =>
		{
			callCount++;
			if (callCount > 1)
			{
				return Task.FromResult(Response(HttpStatusCode.OK));
			}

			var response = Response(HttpStatusCode.ServiceUnavailable);
			if (retryAfter is not null)
			{
				response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
			}

			return Task.FromResult(response);
		}, new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 3 }, (delay, _) =>
		{
			delays.Add(delay);
			return Task.CompletedTask;
		});

		await client.GetAsync("https://ice.example.test/quote");

		Assert.Multiple(() =>
		{
			Assert.That(callCount, Is.EqualTo(2));
			Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(3) }));
		});
	}

	[Test]
	public void GetAsync_should_propagate_cancellation_during_local_retry_delay()
	{
		using var cancellationTokenSource = new CancellationTokenSource();
		var delays = new List<TimeSpan>();
		using var client = CreateClient((_, _) =>
			Task.FromResult(Response(HttpStatusCode.ServiceUnavailable)),
			new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 30 },
			(delay, cancellationToken) =>
			{
				delays.Add(delay);
				cancellationTokenSource.Cancel();
				return Task.Delay(delay, cancellationToken);
			});

		Assert.That(async () => await client.GetAsync(
			"https://ice.example.test/quote",
			cancellationTokenSource.Token), Throws.InstanceOf<OperationCanceledException>());
		Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(30) }));
	}

	[Test]
	public void GetAsync_should_exhaust_transient_http_retries()
	{
		var callCount = 0;
		using var client = CreateClient((_, _) =>
		{
			callCount++;
			return Task.FromResult(Response(HttpStatusCode.ServiceUnavailable));
		}, new RetrySettings { ApiRetryCount = 2, ApiRetryDelaySeconds = 0 }, (_, _) => Task.CompletedTask);

		Assert.ThrowsAsync<HttpRequestException>(async () =>
			await client.GetAsync("https://ice.example.test/quote"));
		Assert.That(callCount, Is.EqualTo(3));
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
		Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseHandler,
		RetrySettings? retrySettings = null,
		Func<TimeSpan, CancellationToken, Task>? retryDelayAsync = null)
	{
		var defaultRetrySettings = new RetrySettings
		{
			ApiRetryCount = 2,
			ApiRetryDelaySeconds = 0
		};
		var iceSettings = new IceSettings
		{
			ApiKeyHeaderName = "X-ICE-API-KEY",
			ApiKeyHeaderValue = "test-key"
		};
		var httpClient = new HttpClient(new StubHttpMessageHandler(responseHandler));
		return new IceApiClient(
			iceSettings,
			NullLogger<IceApiClient>.Instance,
			retrySettings ?? defaultRetrySettings,
			httpClient,
			retryDelayAsync);
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
