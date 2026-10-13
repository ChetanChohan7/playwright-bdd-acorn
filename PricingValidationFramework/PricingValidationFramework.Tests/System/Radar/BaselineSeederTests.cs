using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Seeding;
using PricingValidationFramework.Core.Validation;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class BaselineSeederTests
{
    private const string ApiKey = "seed-test-key-that-must-not-appear";

    [Test]
    public async Task Seed_should_store_each_radar_response_as_a_pass_baseline_at_the_request_time()
    {
        var store = new FakeStore(Scenario("S1"), Scenario("S2"));
        var requests = new ConcurrentBag<Uri>();
        var seeder = CreateSeeder(store, request =>
        {
            requests.Add(request.RequestUri!);
            return Ok($"<Response><Id>{request.RequestUri!.Query.Length}</Id></Response>");
        });

        var summary = await seeder.RunAsync("2024-03-01Z09:30:45");

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.RequestTime, Is.EqualTo("2024-03-01Z09:00:00"));
            Assert.That(summary.ScenariosWithoutBaseline, Is.EqualTo(2));
            Assert.That(summary.Seeded, Is.EqualTo(2));
            Assert.That(summary.Results.Select(result => result.ScenarioId), Is.EqualTo(new[] { "S1", "S2" }), "reported in read order");
            Assert.That(summary.Results.Select(result => result.RouteId), Is.All.EqualTo("Route001"));
            Assert.That(store.Inserted.Keys, Is.EquivalentTo(new[] { "S1", "S2" }));
            Assert.That(store.Inserted.Values.Select(baseline => baseline.Status), Is.All.EqualTo("PASS"));
            Assert.That(store.Inserted.Values.Select(baseline => baseline.XmlResponse), Is.All.StartsWith("<Response>"));
            Assert.That(requests.Select(uri => Uri.UnescapeDataString(uri.Query)), Is.All.Contain("2024-03-01Z09:00:00"));
            Assert.That(requests.Select(uri => uri.Query), Is.All.Contain("KeyName=shared-home"));
        });
    }

    [Test]
    public async Task Seed_should_do_nothing_when_every_scenario_already_has_a_baseline()
    {
        var calls = 0;
        var summary = await CreateSeeder(new FakeStore(), _ => { calls++; return Ok("<Response />"); }).RunAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.ScenariosWithoutBaseline, Is.Zero);
            Assert.That(summary.Results, Is.Empty);
            Assert.That(calls, Is.Zero);
        });
    }

    [Test]
    public async Task Seed_should_report_an_unrouted_scenario_without_calling_radar_and_carry_on()
    {
        var store = new FakeStore(Scenario("S1", scheme: "UNMAPPED"), Scenario("S2"));
        var calls = 0;
        var summary = await CreateSeeder(store, _ => { Interlocked.Increment(ref calls); return Ok("<Response />"); }).RunAsync(null);

        var unrouted = summary.Results.Single(result => result.ScenarioId == "S1");
        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Failed, Is.EqualTo(1));
            Assert.That(summary.Seeded, Is.EqualTo(1));
            Assert.That(unrouted.Outcome, Is.EqualTo(BaselineSeedOutcome.Failed));
            Assert.That(unrouted.Stage, Is.EqualTo("RouteResolution"));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(store.Inserted.Keys, Is.EquivalentTo(new[] { "S2" }));
        });
    }

    [Test]
    public async Task Seed_should_report_a_route_whose_endpoint_is_not_configured()
    {
        var settings = CreateSettings();
        settings.Routes["Route001"].EndpointName = "Endpoint2";
        var store = new FakeStore(Scenario("S1"));

        var summary = await CreateSeeder(store, _ => Ok("<Response />"), settings).RunAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Results.Single().Stage, Is.EqualTo("EndpointResolution"));
            Assert.That(store.Inserted, Is.Empty);
        });
    }

    [Test]
    public async Task Seed_should_not_store_anything_when_radar_fails_and_should_not_leak_the_api_key()
    {
        var store = new FakeStore(Scenario("S1"));
        var summary = await CreateSeeder(store, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent($"bad key {ApiKey}")
        }).RunAsync(null);

        var result = summary.Results.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(BaselineSeedOutcome.Failed));
            Assert.That(result.Stage, Is.EqualTo("RequestExecution"));
            Assert.That(result.Error, Does.Not.Contain(ApiKey));
            Assert.That(store.Inserted, Is.Empty);
        });
    }

    [Test]
    public async Task Seed_should_report_http_429_for_that_scenario_and_carry_on()
    {
        var store = new FakeStore(Scenario("S1"), Scenario("S2"));
        var summary = await CreateSeeder(store, request =>
            request.Content!.ReadAsStringAsync().GetAwaiter().GetResult().Contains("<Id>S1</Id>", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : Ok("<Response />")).RunAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Results.Single(result => result.ScenarioId == "S1").Stage, Is.EqualTo("Http429"));
            Assert.That(summary.Results.Single(result => result.ScenarioId == "S2").Outcome, Is.EqualTo(BaselineSeedOutcome.Seeded));
        });
    }

    [Test]
    public async Task Seed_should_count_a_baseline_written_by_another_run_as_already_existing_not_failed()
    {
        var store = new FakeStore(Scenario("S1")) { InsertResult = false };

        var summary = await CreateSeeder(store, _ => Ok("<Response />")).RunAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.AlreadyExisted, Is.EqualTo(1));
            Assert.That(summary.Results.Single().Outcome, Is.EqualTo(BaselineSeedOutcome.AlreadyExists));
        });
    }

    [Test]
    public async Task Seed_should_report_a_failed_insert()
    {
        var store = new FakeStore(Scenario("S1")) { InsertFailure = new InvalidOperationException("insert failed") };

        var summary = await CreateSeeder(store, _ => Ok("<Response />")).RunAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Results.Single().Stage, Is.EqualTo("BaselineInsert"));
            Assert.That(summary.Results.Single().Error, Does.Contain("insert failed"));
        });
    }

    [Test]
    public async Task Seed_should_stop_and_report_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new FakeStore(Scenario("S1"), Scenario("S2"));

        var summary = await CreateSeeder(store, _ => Ok("<Response />")).RunAsync(null, cancellationToken: cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Cancelled, Is.True);
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.NotRun, Is.EqualTo(2));
            Assert.That(store.Inserted, Is.Empty);
        });
    }

    [TestCase(0)]
    [TestCase(17)]
    public void Seed_should_reject_an_out_of_range_parallelism(int maxParallel)
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateSeeder(new FakeStore(), _ => Ok("<Response />")).RunAsync(null, maxParallel));
    }

    [Test]
    public void Seed_should_reject_a_badly_formatted_request_time_before_reading_scenarios()
    {
        var store = new FakeStore(Scenario("S1"));

        Assert.ThrowsAsync<ArgumentException>(() => CreateSeeder(store, _ => Ok("<Response />")).RunAsync("2024-03-01 09:00"));
        Assert.That(store.Reads, Is.Zero);
    }

    private static BaselineSeeder CreateSeeder(
        FakeStore store,
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        RadarSettings? settings = null)
    {
        var apiClient = new RadarApiClient(
            new NoWaitRateLimiter(),
            new HttpClient(new StubHandler(respond)),
            NullLogger<RadarApiClient>.Instance,
            new RetrySettings { ApiRetryCount = 0, ApiRetryDelaySeconds = 0, ApiRetryAfterMaxDelaySeconds = 0 },
            (_, _) => Task.CompletedTask);
        var service = new RadarPricingService(settings ?? CreateSettings(), apiClient, new RadarUrlBuilder(), new PricingComparisonService([]));
        return new BaselineSeeder(store, service);
    }

    private static RadarSettings CreateSettings() => new()
    {
        Endpoints = new Dictionary<string, RadarEndpointSettings>(StringComparer.Ordinal)
        {
            ["Endpoint1"] = new() { BaseUrl = "https://radar.example.test/quote", ApiKeyHeaderName = "X-API-KEY", ApiKeyValue = ApiKey }
        },
        Routes = new Dictionary<string, RadarRouteSettings>(StringComparer.Ordinal)
        {
            ["Route001"] = new() { ProductCode = "HOME", SchemeCodes = ["ABC", "XYS"], EndpointName = "Endpoint1", RouteKey = "shared-home" }
        }
    };

    private static ScenarioRequest Scenario(string id, string scheme = "ABC") => new()
    {
        ScenarioId = id,
        QuoteRef = $"Q-{id}",
        ProductCode = "HOME",
        SchemeCode = scheme,
        XmlRequest = $"<Request><Id>{id}</Id></Request>"
    };

    private static HttpResponseMessage Ok(string xml) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(xml, global::System.Text.Encoding.UTF8, "application/xml")
    };

    private sealed class FakeStore(params ScenarioRequest[] scenarios) : IBaselineSeedStore
    {
        public ConcurrentDictionary<string, ScenarioResponse> Inserted { get; } = new();
        public bool InsertResult { get; init; } = true;
        public Exception? InsertFailure { get; init; }
        public int Reads { get; private set; }

        public Task<IReadOnlyList<ScenarioRequest>> GetScenariosWithoutBaselineAsync(CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult<IReadOnlyList<ScenarioRequest>>(scenarios);
        }

        public Task<bool> TryInsertBaselineAsync(ScenarioResponse baseline, CancellationToken cancellationToken = default)
        {
            if (InsertFailure is not null)
            {
                throw InsertFailure;
            }

            if (InsertResult)
            {
                Inserted[baseline.ScenarioId] = baseline;
            }

            return Task.FromResult(InsertResult);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class NoWaitRateLimiter : IRadarRequestRateLimiter
    {
        public Task<HttpResponseMessage> SendAsync(string endpointName, Func<Task<HttpResponseMessage>> sendAsync, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return sendAsync();
        }
    }
}
