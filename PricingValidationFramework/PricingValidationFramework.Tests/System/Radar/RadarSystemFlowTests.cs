using System.Net;
using System.Text;
using System.Collections.Concurrent;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Extraction;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Logging;
using PricingValidationFramework.Core.Matching;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Validation;
using PricingValidationFramework.Tests.Helpers.Reporting;
using PricingValidationFramework.Tests.Helpers.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class RadarSystemFlowTests
{
    [Test]
    public async Task Radar_system_flow_should_pass_and_populate_the_final_row()
    {
        var handler = new StubHttpMessageHandler(_ => SuccessResponse("101.00"));
        var (processor, apiClient) = CreateProcessor(handler);
        using var _ = apiClient;

        var row = await processor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-001", "QUOTE-001"),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime);

        Assert.Multiple(() =>
        {
            Assert.That(row.BuildId, Is.EqualTo("build-1"));
            Assert.That(row.ScenarioId, Is.EqualTo("SCENARIO-001"));
            Assert.That(row.QuoteRef, Is.EqualTo("QUOTE-001"));
            Assert.That(row.SchemeCode, Is.EqualTo("ABC"));
            Assert.That(row.ProductCode, Is.EqualTo("HOME"));
            Assert.That(row.RequestXml, Is.EqualTo(RequestXml));
            Assert.That(row.RadarResponseXml, Is.EqualTo(ResponseXml("101.00")));
            Assert.That(row.RadarValue, Is.EqualTo(101.00m));
            Assert.That(row.BaselineValue, Is.EqualTo(100.00m));
            Assert.That(row.Difference, Is.EqualTo(1.00m));
            Assert.That(row.MinThreshold, Is.EqualTo(-5.00m));
            Assert.That(row.MaxThreshold, Is.EqualTo(5.00m));
            Assert.That(row.Result, Is.EqualTo(ScenarioResult.Pass));
        });
    }

    [Test]
    public async Task Radar_system_flow_should_fail_outside_thresholds_and_retain_values()
    {
        var summary = new ValidationSummary();
        var (processor, apiClient) = CreateProcessor(new StubHttpMessageHandler(_ => SuccessResponse("120.00")));
        using var _ = apiClient;

        var row = await processor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-002", "QUOTE-002"),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime);

        Assert.Multiple(() =>
        {
            Assert.That(row.Result, Is.EqualTo(ScenarioResult.Fail));
            Assert.That(row.RadarValue, Is.EqualTo(120.00m));
            Assert.That(row.BaselineValue, Is.EqualTo(100.00m));
            Assert.That(row.Difference, Is.EqualTo(20.00m));
            Assert.That(row.RequestXml, Is.EqualTo(RequestXml));
            Assert.That(row.RadarResponseXml, Is.EqualTo(ResponseXml("120.00")));
        });
        AddFailureIfNeeded(summary, row);
        AssertFailureContains(summary, "ScenarioId=SCENARIO-002, Result=Fail");
    }

    [Test]
    public async Task Radar_system_flow_should_match_route_fields_with_an_arbitrary_route_code()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return SuccessResponse("100.00");
        });
        var (processor, apiClient) = CreateProcessor(handler);
        using var _ = apiClient;

        var row = await processor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-003", "QUOTE-003"),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime);

        Assert.Multiple(() =>
        {
            Assert.That(row.Result, Is.EqualTo(ScenarioResult.Pass));
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.RequestUri!.Query, Does.Contain("KeyName=home-abc"));
            Assert.That(capturedRequest.RequestUri.Query, Does.Contain("KeyRequestTime=2024-03-01Z09%3A30%3A45"));
            Assert.That(capturedRequest.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(capturedRequest.Headers.GetValues("X-API-KEY").Single(), Is.EqualTo("development-placeholder-value-1"));
        });
    }

    [Test]
    public async Task Radar_system_flow_should_resolve_logical_endpoint_names_case_insensitively()
    {
        var settings = CreateSettings();
        settings.Routes["Route001"].EndpointName = "endpoint1";
        var (processor, apiClient) = CreateProcessor(
            new StubHttpMessageHandler(_ => SuccessResponse("100.00")),
            settings);
        using var _ = apiClient;

        var row = await processor.ProcessAsync(
            CreateScenario("SCENARIO-ENDPOINT-CASE", "QUOTE-ENDPOINT-CASE"),
            "build-1",
            100.00m,
            -5.00m,
            5.00m,
            RequestTime);

        Assert.That(row.Result, Is.EqualTo(ScenarioResult.Pass));
    }

    [Test]
    public async Task Radar_system_flow_should_skip_response_xsd_validation_when_no_mapping_exists()
    {
        var settings = CreateSettings();
        settings.ResponseXsdMappings.Remove("Route001");
        var (processor, apiClient) = CreateProcessor(
            new StubHttpMessageHandler(_ => SuccessResponse("101.00")),
            settings);
        using var _ = apiClient;

        var row = await processor.ProcessAsync(
            CreateScenario("SCENARIO-NO-XSD", "QUOTE-NO-XSD"),
            "build-1",
            100.00m,
            -5.00m,
            5.00m,
            RequestTime);

        Assert.Multiple(() =>
        {
            Assert.That(row.Result, Is.EqualTo(ScenarioResult.Pass));
            Assert.That(row.RadarValue, Is.EqualTo(101.00m));
        });
    }

    [Test]
    public async Task Radar_system_flow_should_validate_response_xml_before_deserializing()
    {
        var requestCount = 0;
        var settings = CreateSettings();
        settings.ResponseXsdMappings["Route001"] = "RadarSystemTest.xsd";
        var (processor, apiClient) = CreateProcessor(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return RawResponse("<Response><Invalid>oops</Invalid></Response>");
        }), settings);
        using var _ = apiClient;

        var row = await processor.ProcessAsync(
            CreateScenario("SCENARIO-RESPONSE-XSD", "QUOTE-RESPONSE-XSD"),
            "build-1",
            100.00m,
            -5.00m,
            5.00m,
            RequestTime);

        Assert.Multiple(() =>
        {
            Assert.That(row.Result, Is.EqualTo(ScenarioResult.Error));
            Assert.That(requestCount, Is.EqualTo(1));
        });
    }

    [TestCase("<Response />")]
    [TestCase("<Response><TotalAmount>1</TotalAmount><TotalAmount>2</TotalAmount></Response>")]
    public async Task Radar_system_flow_should_error_when_typed_response_total_amount_is_missing_or_duplicated(string responseXml)
    {
        var settings = CreateSettings();
        settings.ResponseXsdMappings.Remove("Route001");
        var (processor, apiClient) = CreateProcessor(new StubHttpMessageHandler(_ => RawResponse(responseXml)), settings);
        using var _ = apiClient;

        var row = await processor.ProcessAsync(
            CreateScenario("SCENARIO-MODEL-ERROR", "QUOTE-MODEL-ERROR"),
            "build-1",
            100.00m,
            -5.00m,
            5.00m,
            RequestTime);

        Assert.That(row.Result, Is.EqualTo(ScenarioResult.Error));
    }

    [Test]
    public async Task Radar_system_flow_should_record_error_without_calling_client_when_route_or_endpoint_is_missing()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("client should not be called"));
        var settings = CreateSettings();
        settings.Routes.Remove("Route001");
        var missingRouteSummary = new ValidationSummary();
        var (processor, firstApiClient) = CreateProcessor(handler, settings);
        using var firstClient = firstApiClient;

        var missingRouteRow = await processor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-004", "QUOTE-004"),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime);

        settings = CreateSettings();
        settings.Endpoints.Clear();
        var missingEndpointSummary = new ValidationSummary();
        var (secondProcessor, secondApiClient) = CreateProcessor(handler, settings);
        using var secondClient = secondApiClient;
        processor = secondProcessor;
        var missingEndpointRow = await processor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-005", "QUOTE-005"),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime);

        Assert.Multiple(() =>
        {
            AssertErrorRow(missingRouteRow);
            AssertErrorRow(missingEndpointRow);
        });
        AddFailureIfNeeded(missingRouteSummary, missingRouteRow);
        AddFailureIfNeeded(missingEndpointSummary, missingEndpointRow);
        AssertFailureContains(missingRouteSummary, "ScenarioId=SCENARIO-004");
        AssertFailureContains(missingEndpointSummary, "ScenarioId=SCENARIO-005");
    }

    [Test]
    public async Task Radar_system_flow_should_record_error_for_duplicate_route_fields()
    {
        var settings = CreateSettings();
        settings.Routes["Route002"] = new RadarRouteSettings
        {
            ProductCode = "HOME",
            SchemeCode = "ABC",
            EndpointName = "Endpoint1",
            RouteKey = "another-home-abc"
        };
        var summary = new ValidationSummary();
        var (processor, apiClient) = CreateProcessor(
            new StubHttpMessageHandler(_ => throw new InvalidOperationException("client should not be called")),
            settings);
        using var _ = apiClient;

        var row = await processor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-006", "QUOTE-006"),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime);

        Assert.That(row.Result, Is.EqualTo(ScenarioResult.Error));
        AddFailureIfNeeded(summary, row);
        AssertFailureContains(summary, "ScenarioId=SCENARIO-006");
    }

    [Test]
    public async Task Radar_system_flow_should_trim_database_values_without_changing_case()
    {
        var (processor, apiClient) = CreateProcessor(new StubHttpMessageHandler(_ => SuccessResponse("101.00")));
        using var _ = apiClient;

        var row = await processor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-007", "QUOTE-007", " HOME ", " ABC "),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime);

        Assert.That(row.Result, Is.EqualTo(ScenarioResult.Pass));
    }

    [Test]
    public async Task Radar_system_flow_should_not_match_route_fields_with_different_case()
    {
        var summary = new ValidationSummary();
        var (processor, apiClient) = CreateProcessor(
            new StubHttpMessageHandler(_ => throw new InvalidOperationException("client should not be called")));
        using var _ = apiClient;

        var row = await processor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-008", "QUOTE-008", "HOME", "abc"),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime);

        Assert.That(row.Result, Is.EqualTo(ScenarioResult.Error));
        AddFailureIfNeeded(summary, row);
        AssertFailureContains(summary, "ScenarioId=SCENARIO-008");
    }

    [Test]
    public async Task Radar_system_flow_should_record_error_for_http_failure_and_empty_response()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/xml")
            }
        });
        var summary = new ValidationSummary();
        var (processor, apiClient) = CreateProcessor(new StubHttpMessageHandler(_ => responses.Dequeue()));
        using var _ = apiClient;

        var httpFailureRow = await processor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-009", "QUOTE-009"),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime);
        var emptyResponseRow = await processor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-010", "QUOTE-010"),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime);

        Assert.Multiple(() =>
        {
            AssertErrorRow(httpFailureRow);
            AssertErrorRow(emptyResponseRow);
        });
        AddFailureIfNeeded(summary, httpFailureRow);
        AddFailureIfNeeded(summary, emptyResponseRow);
        AssertFailureContains(summary, "ScenarioId=SCENARIO-009");
        AssertFailureContains(summary, "ScenarioId=SCENARIO-010");
    }

    [Test]
    public async Task Radar_system_flow_should_record_xsd_error_before_extraction()
    {
        var xsdSummary = new ValidationSummary();
        var (xsdProcessor, apiClient) = CreateProcessor(
            new StubHttpMessageHandler(_ => RawResponse("<Response><Invalid>oops</Invalid></Response>")));
        using var _ = apiClient;

        var xsdRow = await xsdProcessor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-011", "QUOTE-011"),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime);

        AddFailureIfNeeded(xsdSummary, xsdRow);
        var xsdFailure = GetFailureText(xsdSummary);
        Assert.Multiple(() =>
        {
            Assert.That(xsdRow.Result, Is.EqualTo(ScenarioResult.Error));
            Assert.That(xsdRow.RadarResponseXml, Is.EqualTo("<Response><Invalid>oops</Invalid></Response>"));
            Assert.That(xsdFailure, Does.Contain("ScenarioId=SCENARIO-011"));
        });
    }

    [Test]
    public void Radar_system_flow_should_preserve_cancellation_as_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (processor, apiClient) = CreateProcessor(new StubHttpMessageHandler(_ => throw new InvalidOperationException("client should not be called")));
        using var _ = apiClient;

        Assert.That(async () => await processor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-012", "QUOTE-012"),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime,
            cancellationToken: cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task Radar_system_flow_should_continue_after_fail_and_error_and_collect_all_rows()
    {
        var callCount = 0;
        var (processor, apiClient) = CreateProcessor(new StubHttpMessageHandler(_ =>
        {
            callCount++;
            return callCount == 1
                ? SuccessResponse("120.00")
                : callCount <= 5
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    : SuccessResponse("101.00");
        }));
        using var _ = apiClient;

        var rows = new List<RadarValidationReportRow>();
        for (var index = 0; index < 3; index++)
        {
            rows.Add(await processor.ProcessAsync(
                scenario: CreateScenario($"SCENARIO-{index + 13:000}", $"QUOTE-{index + 13:000}"),
                buildId: "build-1",
                baselineValue: 100.00m,
                minThreshold: -5.00m,
                maxThreshold: 5.00m,
                requestTime: RequestTime));
        }

        Assert.That(rows.Select(row => row.Result), Is.EqualTo(new[]
        {
            ScenarioResult.Fail,
            ScenarioResult.Error,
            ScenarioResult.Pass
        }));
        Assert.That(rows[2].RadarValue, Is.EqualTo(101.00m));
    }

    [Test]
    public async Task Radar_system_flow_should_isolate_concurrent_scenarios_and_complete_independent_cases()
    {
        var observedRequests = new ConcurrentBag<(string ApiKey, string Body)>();
        var handler = new StubHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            observedRequests.Add((request.Headers.GetValues("X-API-KEY").Single(), body));
            return body.Contains("FAIL-ME", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.BadRequest)
                : SuccessResponse("101.00");
        });
        var (processor, apiClient) = CreateProcessor(handler);
        using var _ = apiClient;

        var failingScenario = CreateScenario("SCENARIO-PARALLEL-FAIL", "QUOTE-FAIL");
        failingScenario.XmlRequest = "<Request>FAIL-ME</Request>";
        var passingScenario = CreateScenario("SCENARIO-PARALLEL-PASS", "QUOTE-PASS");
        passingScenario.XmlRequest = "<Request>PASS-ME</Request>";

        var rows = await Task.WhenAll(
            processor.ProcessAsync(failingScenario, "build-1", 100.00m, -5.00m, 5.00m, RequestTime),
            processor.ProcessAsync(passingScenario, "build-1", 100.00m, -5.00m, 5.00m, RequestTime));

        Assert.Multiple(() =>
        {
            Assert.That(rows.Single(row => row.ScenarioId == "SCENARIO-PARALLEL-FAIL").Result, Is.EqualTo(ScenarioResult.Error));
            Assert.That(rows.Single(row => row.ScenarioId == "SCENARIO-PARALLEL-PASS").Result, Is.EqualTo(ScenarioResult.Pass));
            Assert.That(observedRequests, Does.Contain(("development-placeholder-value-1", "<Request>FAIL-ME</Request>")));
            Assert.That(observedRequests, Does.Contain(("development-placeholder-value-1", "<Request>PASS-ME</Request>")));
        });
    }

    [Test]
    public async Task Radar_system_flow_should_return_error_for_transport_failure_without_summary_ownership()
    {
        var (processor, apiClient) = CreateProcessor(
            new StubHttpMessageHandler(_ => throw new InvalidOperationException("transport failure: development-placeholder-value-1")));
        using var _ = apiClient;

        var row = await processor.ProcessAsync(
            scenario: CreateScenario("SCENARIO-016", "QUOTE-016"),
            buildId: "build-1",
            baselineValue: 100.00m,
            minThreshold: -5.00m,
            maxThreshold: 5.00m,
            requestTime: RequestTime);

        Assert.That(row.Result, Is.EqualTo(ScenarioResult.Error));
    }

    [Test]
    public async Task Radar_system_flow_should_serialize_pass_fail_and_error_rows()
    {
        var rows = new[]
        {
            RadarReportingHelper.BuildRow("build-1", "SCENARIO-001", "QUOTE-001", "ABC", "HOME", RequestXml, ResponseXml("100.00"), 100.00m, 100.00m, 0.00m, -5.00m, 5.00m, ScenarioResult.Pass),
            RadarReportingHelper.BuildRow("build-1", "SCENARIO-002", "QUOTE-002", "ABC", "HOME", RequestXml, ResponseXml("120.00"), 120.00m, 100.00m, 20.00m, -5.00m, 5.00m, ScenarioResult.Fail),
            RadarReportingHelper.BuildRow("build-1", "SCENARIO-003", "QUOTE-003", "ABC", "HOME", RequestXml, string.Empty, null, 100.00m, null, -5.00m, 5.00m, ScenarioResult.Error)
        };

        await RadarReportingHelper.WriteReportAsync("build-1", rows, CancellationToken.None);
        var reportPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestResults", "Reports", "Radar_build-1.csv");

        try
        {
            var csv = await File.ReadAllTextAsync(reportPath);
            Assert.Multiple(() =>
            {
                Assert.That(csv, Does.Contain("PASS"));
                Assert.That(csv, Does.Contain("FAIL"));
                Assert.That(csv, Does.Contain("ERROR"));
            });
        }
        finally
        {
            if (File.Exists(reportPath))
            {
                File.Delete(reportPath);
            }
        }
    }

    private const string RequestTime = "2024-03-01Z09:30:45";
    private const string RequestXml = "<Request><TotalAmount>100.00</TotalAmount></Request>";

    private static ScenarioRequest CreateScenario(
        string scenarioId,
        string quoteRef,
        string productCode = "HOME",
        string schemeCode = "ABC")
    {
        return new ScenarioRequest
        {
            ScenarioId = scenarioId,
            QuoteRef = quoteRef,
            ProductCode = productCode,
            SchemeCode = schemeCode,
            XmlRequest = RequestXml
        };
    }

    private static (RadarScenarioProcessor Processor, RadarApiClient ApiClient) CreateProcessor(
        StubHttpMessageHandler handler,
        RadarSettings? settings = null)
    {
        var apiClient = new RadarApiClient(
            new CountingRateLimiter(),
            new RestClient(new HttpClient(handler)),
            NullLogger<RadarApiClient>.Instance,
            new RetrySettings
            {
                ApiRetryCount = 2,
                ApiRetryDelaySeconds = 0
            });
        var processor = new RadarScenarioProcessor(
            settings ?? CreateSettings(),
            apiClient,
            new RadarUrlBuilder(),
            new XsdFileResolver(),
            new XsdValidator(),
            new ThresholdMatcher(),
            new RadarTestRunLogger(NullLogger<RadarTestRunLogger>.Instance));

        return (processor, apiClient);
    }

    private static RadarSettings CreateSettings()
    {
        return new RadarSettings
        {
            Endpoints = new Dictionary<string, RadarEndpointSettings>(StringComparer.Ordinal)
            {
                ["Endpoint1"] = new()
                {
                    BaseUrl = "https://placeholder-radar-1.example.com/quote",
                    ApiKeyHeaderName = "X-API-KEY",
                    ApiKeyValue = "development-placeholder-value-1"
                }
            },
            Routes = new Dictionary<string, RadarRouteSettings>(StringComparer.Ordinal)
            {
                ["Route001"] = new()
                {
                    ProductCode = "HOME",
                    SchemeCode = "ABC",
                    EndpointName = "Endpoint1",
                    RouteKey = "home-abc"
                }
            },
            ResponseXsdMappings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Route001"] = "RadarSystemTest.xsd"
            }
        };
    }

    private static void AssertErrorRow(RadarValidationReportRow row)
    {
        Assert.Multiple(() =>
        {
            Assert.That(row.Result, Is.EqualTo(ScenarioResult.Error));
            Assert.That(row.RequestXml, Is.EqualTo(RequestXml));
            Assert.That(row.RadarResponseXml, Is.Empty);
            Assert.That(row.RadarValue, Is.Null);
            Assert.That(row.Difference, Is.Null);
            Assert.That(row.BaselineValue, Is.EqualTo(100.00m));
            Assert.That(row.MinThreshold, Is.EqualTo(-5.00m));
            Assert.That(row.MaxThreshold, Is.EqualTo(5.00m));
        });
    }

    private static void AssertFailureContains(ValidationSummary summary, string expectedText)
    {
        Assert.That(GetFailureText(summary), Does.Contain(expectedText));
    }

    private static void AddFailureIfNeeded(ValidationSummary summary, RadarValidationReportRow row)
    {
        if (row.Result != ScenarioResult.Pass)
        {
            summary.AddFailure($"ScenarioId={row.ScenarioId}, Result={row.Result}");
        }
    }

    private static string GetFailureText(ValidationSummary summary)
    {
        var failure = Assert.Throws<AssertionException>(() => summary.AssertNoFailures());
        return failure!.Message;
    }

    private static HttpResponseMessage SuccessResponse(string amount)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ResponseXml(amount), Encoding.UTF8, "application/xml")
        };
    }

    private static HttpResponseMessage RawResponse(string responseXml)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseXml, Encoding.UTF8, "application/xml")
        };
    }

    private static string ResponseXml(string amount)
    {
        return $"<Response><TotalAmount>{amount}</TotalAmount></Response>";
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class CountingRateLimiter : IRadarRequestRateLimiter
    {
        public ValueTask WaitAsync(string endpointName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
}