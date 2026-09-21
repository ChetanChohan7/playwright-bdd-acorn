using System.Net;
using FuzzyPricingMatcher.Tests.ExternalAPIAccess;
using FuzzyPricingMatcher.Tests.Configuration;
using NUnit.Framework;
using RestSharp;
using RestSharp.Authenticators;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

public sealed class ApiFoundationTests
{
    [Test]
    [Category("Unit")]
    public void Rest_client_factory_reuses_one_client_per_endpoint_and_configures_basic_authentication()
    {
        using var factory = new ExternalServiceClientFactory();
        var endpoint = Endpoint("https://api.example.test", "Path", "yyyy-MM-dd", "__NOT_A_REAL_USERNAME__", "__NOT_A_REAL_PASSWORD__");
        var first = factory.GetClient("EndpointA", endpoint);
        var second = factory.GetClient("EndpointA", endpoint);
        var other = factory.GetClient("EndpointB", endpoint);
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.SameAs(second));
            Assert.That(other, Is.Not.SameAs(first));
            Assert.That(first.Options.Authenticator, Is.TypeOf<HttpBasicAuthenticator>());
        });
    }

    [Test]
    [Category("Unit")]
    public void Resource_builder_supports_path_query_and_endpoint_specific_formats()
    {
        var builder = new ExternalAPIRequestUrlBuilder();
        var path = builder.BuildRequestUri(Endpoint("https://api.example.test", "Path", "yyyyMMdd"), new DateOnly(2026, 9, 18));
        var appendedPath = builder.BuildRequestUri(Endpoint("https://api.example.test", "Path", "yyyyMMdd", resource: "/v1/responses/"), new DateOnly(2026, 9, 18));
        var query = builder.BuildRequestUri(Endpoint("https://api.example.test", "QueryString", "dd-MM-yyyy", resource: "/responses"), new DateOnly(2026, 9, 18));
        Assert.Multiple(() =>
        {
            Assert.That(path.ResourcePath, Is.EqualTo("/v1/20260918"));
            Assert.That(path.IsQueryDate, Is.False);
            Assert.That(appendedPath.ResourcePath, Is.EqualTo("/v1/responses/20260918"));
            Assert.That(appendedPath.IsQueryDate, Is.False);
            Assert.That(query.ResourcePath, Is.EqualTo("/responses"));
            Assert.That(query.DateParameterName, Is.EqualTo("date"));
            Assert.That(query.DateValue, Is.EqualTo("18-09-2026"));
        });
    }

    [Test]
    [Category("Unit")]
    public void Blank_api_date_uses_utc_date()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ApiDateResolver.Resolve(null, new DateTimeOffset(2026, 9, 18, 23, 59, 0, TimeSpan.Zero)), Is.EqualTo(new DateOnly(2026, 9, 18)));
            Assert.That(ApiDateResolver.Resolve("2026-09-19"), Is.EqualTo(new DateOnly(2026, 9, 19)));
        });
    }

    [Test]
    [Category("Unit")]
    public async Task Xml_client_sends_exact_raw_body_and_query_date()
    {
        var executor = new FakeExecutor();
        var limiter = new FakeLimiter();
        var client = CreateClient(executor, limiter, Endpoint("https://api.example.test", "QueryString", "yyyy-MM-dd", resource: "/responses"));
        var raw = "<Request>  <SchemeCode>S-1</SchemeCode>\n<PolicyReference>Q-1</PolicyReference> </Request>";
        var result = await client.SendXmlRequestAsync(Request(raw, Endpoint("https://api.example.test", "QueryString", "yyyy-MM-dd", resource: "/responses")));
        var request = executor.Requests.Single();
        var body = request.Parameters.Single(parameter => parameter.Type == ParameterType.RequestBody).Value?.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(result.Successful, Is.True);
            Assert.That(body, Is.EqualTo(raw));
            Assert.That(request.Parameters.Single(parameter => parameter.Name == "date").Value, Is.EqualTo("2026-09-18"));
            Assert.That(executor.Requests, Has.Count.EqualTo(1));
            Assert.That(limiter.PermitCount, Is.EqualTo(1));
        });
    }

    [Test]
    [Category("Unit")]
    public async Task Retryable_statuses_create_new_requests_and_acquire_each_permit()
    {
        var executor = new FakeExecutor { Statuses = new[] { 503, 502, 200 } };
        var limiter = new FakeLimiter();
        var settings = new ResilienceSettings { ApiRetryAttempts = 2, ApiRetryInitialDelaySeconds = 0, ApiRetryMaximumDelaySeconds = 0 };
        var pipeline = new ExternalAPIRetryPolicy(settings, limiter, (_, _) => Task.CompletedTask);
        var client = CreateClient(executor, limiter, Endpoint("https://api.example.test", "Path", "yyyy-MM-dd"), pipeline);
        var result = await client.SendXmlRequestAsync(Request("<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference></Request>", Endpoint("https://api.example.test", "Path", "yyyy-MM-dd")));
        Assert.Multiple(() =>
        {
            Assert.That(result.Successful, Is.True);
            Assert.That(executor.Requests, Has.Count.EqualTo(3));
            Assert.That(executor.Requests[0], Is.Not.SameAs(executor.Requests[1]));
            Assert.That(limiter.PermitCount, Is.EqualTo(3));
        });
    }

    [Test]
    [Category("Unit")]
    public async Task Non_retryable_status_and_cancellation_do_not_retry()
    {
        var executor = new FakeExecutor { Statuses = new[] { 400 } };
        var limiter = new FakeLimiter();
        var client = CreateClient(executor, limiter, Endpoint("https://api.example.test", "Path", "yyyy-MM-dd"));
        var result = await client.SendXmlRequestAsync(Request("<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference></Request>", Endpoint("https://api.example.test", "Path", "yyyy-MM-dd")));
        Assert.That(executor.Requests, Has.Count.EqualTo(1));
        Assert.That(result.Successful, Is.False);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.That(async () => await client.SendXmlRequestAsync(Request("<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference></Request>", Endpoint("https://api.example.test", "Path", "yyyy-MM-dd")), cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    [Category("Unit")]
    public async Task Retry_after_and_transport_failures_are_supported_without_network_access()
    {
        var attempts = 0;
        var limiter = new FakeLimiter();
        var delays = new List<TimeSpan>();
        var pipeline = new ExternalAPIRetryPolicy(new ResilienceSettings { ApiRetryAttempts = 1, ApiRetryInitialDelaySeconds = 0, ApiRetryMaximumDelaySeconds = 10 }, limiter, (delay, _) => { delays.Add(delay); return Task.CompletedTask; });
        var result = await pipeline.ExecuteAsync("retry-after", (_, _) =>
        {
            attempts++;
            return Task.FromResult(attempts == 1 ? ExternalAPIResponse.Failure(429, "rate limited", attempts, TimeSpan.Zero, TimeSpan.FromSeconds(3)) : ExternalAPIResponse.Success(200, "ok", attempts, TimeSpan.Zero));
        });
        Assert.Multiple(() =>
        {
            Assert.That(result.Successful, Is.True);
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(limiter.PermitCount, Is.EqualTo(2));
            Assert.That(delays.Single(), Is.EqualTo(TimeSpan.FromSeconds(3)));
        });
    }

    [Test]
    [Category("Unit")]
    public async Task Default_rate_limiter_spaces_starts_by_approximately_500_milliseconds()
    {
        var clock = new FakeClock();
        var limiter = new ExternalAPIRateLimiter(2, clock);
        await limiter.WaitAsync();
        await limiter.WaitAsync();
        Assert.That(clock.Delays.Single(), Is.EqualTo(TimeSpan.FromMilliseconds(500)));
    }

    [Test]
    [Category("Unit")]
    public async Task Transport_exception_is_retried_and_each_attempt_gets_a_permit()
    {
        var attempts = 0;
        var limiter = new FakeLimiter();
        var pipeline = new ExternalAPIRetryPolicy(new ResilienceSettings { ApiRetryAttempts = 1, ApiRetryInitialDelaySeconds = 0, ApiRetryMaximumDelaySeconds = 0 }, limiter, (_, _) => Task.CompletedTask);
        var result = await pipeline.ExecuteAsync("transport", (_, _) =>
        {
            attempts++;
            if (attempts == 1) throw new ExternalAPITransportException("transport");
            return Task.FromResult(ExternalAPIResponse.Success(200, "ok", attempts, TimeSpan.Zero));
        });
        Assert.Multiple(() =>
        {
            Assert.That(result.Successful, Is.True);
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(limiter.PermitCount, Is.EqualTo(2));
        });
    }

    [Test]
    [Category("Unit")]
    public void Disabled_invalid_and_incomplete_endpoints_are_rejected_before_network_access()
    {
        using var factory = new ExternalServiceClientFactory();
        Assert.Multiple(() =>
        {
            Assert.That(() => factory.GetClient("disabled", Endpoint("https://api.example.test", "Path", "yyyy-MM-dd", enabled: false)), Throws.TypeOf<ExternalAPIConfigurationException>());
            Assert.That(() => factory.GetClient("invalid", Endpoint("https://endpoint.invalid", "Path", "yyyy-MM-dd")), Throws.TypeOf<ExternalAPIConfigurationException>());
            Assert.That(() => factory.GetClient("missing", Endpoint("https://api.example.test", "Path", "yyyy-MM-dd", resource: "")), Throws.TypeOf<ExternalAPIConfigurationException>());
            Assert.That(() => factory.GetClient("credentials", Endpoint("https://api.example.test", "Path", "yyyy-MM-dd", username: "", password: "")), Throws.TypeOf<ExternalAPIConfigurationException>());
            Assert.That(() => new ExternalAPIRequestUrlBuilder().BuildRequestUri(Endpoint("https://api.example.test", "Header", "yyyy-MM-dd"), new DateOnly(2026, 1, 1)), Throws.TypeOf<ExternalAPIConfigurationException>());
        });
    }

    private static ExternalXmlServiceClient CreateClient(FakeExecutor executor, FakeLimiter limiter, EndpointSettings endpoint, ExternalAPIRetryPolicy? pipeline = null) => new(new ExternalServiceClientFactory(), new ExternalAPIRequestUrlBuilder(), pipeline ?? new ExternalAPIRetryPolicy(new ResilienceSettings { ApiRetryAttempts = 0 }, limiter, (_, _) => Task.CompletedTask), executor);

    private static ExternalXmlRequest Request(string raw, EndpointSettings endpoint) => new("SCN-1", "S-1", raw, "BUILD-1", endpoint, new RouteSettings { EndpointName = "EndpointA", Enabled = true }, new DateOnly(2026, 9, 18));

    private static EndpointSettings Endpoint(string baseUrl, string placement, string format, string username = "__NOT_A_REAL_USERNAME__", string password = "__NOT_A_REAL_PASSWORD__", bool enabled = true, string resource = "/v1/{date}") => new() { BaseUrl = baseUrl, Resource = resource, Username = username, Password = password, DatePlacement = placement, DateParameterName = "date", DateFormat = format, ContentType = "application/xml", Enabled = enabled };

    private sealed class FakeLimiter : IExternalAPIRateLimiter
    {
        public int PermitCount { get; private set; }
        public Task WaitAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); PermitCount++; return Task.CompletedTask; }
    }

    private sealed class FakeExecutor : IRestRequestExecutor
    {
        public int[] Statuses { get; set; } = new[] { 200 };
        public List<RestRequest> Requests { get; } = [];
        private int callCount;
        public Task<RestResponse> ExecuteAsync(RestClient client, RestRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var status = Statuses[Math.Min(callCount++, Statuses.Length - 1)];
            return Task.FromResult(new RestResponse(request) { StatusCode = (HttpStatusCode)status, Content = "<response />" });
        }
    }

    private sealed class FakeClock : IApiClock
    {
        public DateTimeOffset Current { get; private set; } = DateTimeOffset.UtcNow;
        public List<TimeSpan> Delays { get; } = [];
        public DateTimeOffset UtcNow => Current;
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) { Delays.Add(delay); Current += delay; return Task.CompletedTask; }
    }
}
