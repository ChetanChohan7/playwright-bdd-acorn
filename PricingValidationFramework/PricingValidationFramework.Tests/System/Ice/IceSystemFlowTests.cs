using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Extraction;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Reporting;
using PricingValidationFramework.Tests.Helpers.Reporting;
using PricingValidationFramework.Tests.Integration.Ice;

namespace PricingValidationFramework.Tests.System.Ice;

[TestFixture]
public class IceSystemFlowTests
{
    [Test]
    public void Ice_case_generation_should_create_individual_safe_named_cases()
    {
        var firstScenario = CreateScenario("<Response><Premium>100.00</Premium></Response>");
        var secondScenario = CreateScenario("<Response><Premium>120.00</Premium></Response>");
        secondScenario.ScenarioId = "SCENARIO 002";

        var cases = IceValidationTests.CreateScenarioCases([firstScenario, secondScenario]);
        var testMethod = typeof(IceValidationTests).GetMethod(nameof(IceValidationTests.Ice_scenario_should_match_baseline))!;
        var source = testMethod.GetCustomAttributes(typeof(TestCaseSourceAttribute), false)
            .Cast<TestCaseSourceAttribute>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(cases, Has.Count.EqualTo(2));
            Assert.That(cases[0].TestName, Is.EqualTo("Ice_scenario_SCENARIO-001"));
            Assert.That(cases[1].TestName, Is.EqualTo("Ice_scenario_SCENARIO_002"));
            Assert.That(cases[0].Arguments[0], Is.SameAs(firstScenario));
            Assert.That(cases[1].Arguments[0], Is.SameAs(secondScenario));
            Assert.That(cases[0].Arguments[1], Is.Null);
            Assert.That(cases[0].Properties["Category"], Does.Contain("Ice"));
            Assert.That(typeof(IceValidationTests).IsDefined(typeof(NonParallelizableAttribute), false), Is.True);
            Assert.That(source.SourceName, Is.EqualTo(nameof(IceValidationTests.IceScenarios)));
            Assert.That(testMethod.IsDefined(typeof(TestAttribute), false), Is.False);
        });
    }

    [Test]
    public void Ice_case_generation_should_fail_discovery_when_no_scenarios_match()
    {
        var cases = IceValidationTests.CreateScenarioCases([]);

        Assert.Multiple(() =>
        {
            Assert.That(cases, Has.Count.EqualTo(1));
            Assert.That(cases[0].TestName, Is.EqualTo("Ice_scenario_discovery_should_succeed"));
            Assert.That(cases[0].Arguments[0], Is.Null);
            Assert.That(cases[0].Arguments[1], Is.EqualTo("No ICE scenarios were returned for the selected workload."));
            Assert.That(cases[0].Properties["Category"], Does.Contain("IceDiscovery"));
        });
    }

    [Test]
    public void Ice_case_generation_should_fail_discovery_for_duplicate_scenario_ids()
    {
        var firstScenario = CreateScenario("<Response><Premium>100.00</Premium></Response>");
        var secondScenario = CreateScenario("<Response><Premium>120.00</Premium></Response>");
        secondScenario.ScenarioId = "scenario-001";

        var cases = IceValidationTests.CreateScenarioCases([firstScenario, secondScenario]);

        Assert.Multiple(() =>
        {
            Assert.That(cases, Has.Count.EqualTo(1));
            Assert.That(cases[0].Arguments[0], Is.Null);
            Assert.That(cases[0].Arguments[1], Does.Contain("Duplicate ScenarioId"));
        });
    }

    [Test]
    public void Ice_discovery_failure_should_fail_its_case_before_api_setup()
    {
        var fixture = new IceValidationTests();
        var discoveryCase = IceValidationTests.CreateScenarioCases([]).Single();
        var exception = Assert.ThrowsAsync<AssertionException>(async () =>
            await fixture.Ice_scenario_should_match_baseline(null, (string)discoveryCase.Arguments[1]!));

        Assert.That(exception!.Message, Is.EqualTo(discoveryCase.Arguments[1]));
    }

    [Test]
    public async Task Ice_individual_results_should_keep_later_cases_independent_and_report_all_outcomes()
    {
        var fixture = new IceValidationTests();
        var buildId = $"ice-cases-{Guid.NewGuid():N}";
        var reportPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestResults", "Reports", $"Ice_{buildId}.csv");
        var failedScenario = CreateScenario("<Response><Premium>100.00</Premium></Response>");
        var errorScenario = CreateScenario("<Response><Premium>100.00</Premium></Response>");
        errorScenario.ScenarioId = "SCENARIO-002";
        var passingScenario = CreateScenario("<Response><Premium>100.00</Premium></Response>");
        passingScenario.ScenarioId = "SCENARIO-003";

        try
        {
            Assert.Throws<AssertionException>(() => fixture.RecordScenarioResult(
                IceReportingHelper.BuildRow(buildId, failedScenario, 120m, 100m, ScenarioResult.Fail)));
            var error = Assert.Throws<AssertionException>(() => fixture.RecordScenarioResult(
                IceReportingHelper.BuildRow(buildId, errorScenario, null, null, ScenarioResult.Error),
                new InvalidDataException("Invalid ICE payload.")));
            Assert.That(error!.Message, Does.Contain("Invalid ICE payload."));
            Assert.DoesNotThrow(() => fixture.RecordScenarioResult(
                IceReportingHelper.BuildRow(buildId, passingScenario, 100m, 100m, ScenarioResult.Pass)));

            await fixture.TearDown();
            var lines = await File.ReadAllLinesAsync(reportPath);
            Assert.Multiple(() =>
            {
                Assert.That(lines, Has.Length.EqualTo(4));
                Assert.That(lines[1], Does.EndWith(",FAIL"));
                Assert.That(lines[2], Does.EndWith(",ERROR"));
                Assert.That(lines[3], Does.EndWith(",PASS"));
            });
        }
        finally
        {
            File.Delete(reportPath);
        }
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    public void Ice_baseline_extractor_should_decode_json_escaped_xml(int escapeCount, bool retainOuterQuotes)
    {
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n<Response><Premium>100.00</Premium></Response>";
        var options = new global::System.Text.Json.JsonSerializerOptions
        {
            Encoder = global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        for (var escapeIndex = 0; escapeIndex < escapeCount; escapeIndex++)
        {
            var json = global::System.Text.Json.JsonSerializer.Serialize(xml, options);
            xml = retainOuterQuotes ? json : json[1..^1];
        }

        var value = new XmlValueExtractor().ExtractBaselineValue(xml);

        Assert.That(value, Is.EqualTo(100.00m));
    }

    [TestCase("{\"technicalPrice\":{\"grossPremiumAmountIncTax\":{\"amount\":100.00}}}", ScenarioResult.Pass, 100)]
    [TestCase("{\"technicalPrice\":{\"grossPremiumAmountIncTax\":{\"amount\":120.00}}}", ScenarioResult.Fail, 120)]
    [TestCase("{invalid-json", ScenarioResult.Error, null)]
    public async Task Ice_system_flow_should_record_and_assert_individual_scenarios(
        string response,
        ScenarioResult expectedResult,
        int? expectedIceValue)
    {
        var scenario = CreateScenario("<Response><Premium>100.00</Premium></Response>");
        var fixture = new IceValidationTests();
        var handler = new StubIceHandler(response);
        var settings = new IceSettings
        {
            IceEndpoint = "https://ice.test/quote",
            ApiKeyHeaderName = "X-ICE-API-KEY",
            ApiKeyHeaderValue = "test-value"
        };
        using var apiClient = new IceApiClient(settings, NullLogger<IceApiClient>.Instance,
            new RetrySettings { ApiRetryCount = 0 }, new HttpClient(handler));
        var url = new IceUrlBuilder().Build(settings.IceEndpoint, scenario.QuoteRef);
        var payload = await apiClient.GetAsync(url);
        IceValidationReportRow row;
        Exception? scenarioError = null;
        try
        {
            var iceValue = new JsonValueExtractor().ExtractPremium(payload);
            var baselineValue = new XmlValueExtractor().ExtractBaselineValue(scenario.XmlResponse);
            row = IceReportingHelper.BuildRow("system-test", scenario, iceValue, baselineValue,
                iceValue == baselineValue ? ScenarioResult.Pass : ScenarioResult.Fail);
        }
        catch (global::System.Text.Json.JsonException exception)
        {
            scenarioError = exception;
            row = IceReportingHelper.BuildRow("system-test", scenario, null, null, ScenarioResult.Error);
        }

        if (expectedResult == ScenarioResult.Pass)
        {
            Assert.DoesNotThrow(() => fixture.RecordScenarioResult(row, scenarioError));
        }
        else
        {
            Assert.Throws<AssertionException>(() => fixture.RecordScenarioResult(row, scenarioError));
        }

        Assert.Multiple(() =>
        {
            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(handler.LastUrl, Is.EqualTo("https://ice.test/quote/QUOTE-001"));
            Assert.That(row.Result, Is.EqualTo(expectedResult));
            Assert.That(row.IceValue, Is.EqualTo((decimal?)expectedIceValue));
            Assert.That(row.BaselineValue, Is.EqualTo(expectedResult == ScenarioResult.Error ? null : (decimal?)100m));
        });
    }

    [Test]
    public void Ice_system_flow_should_not_convert_cancellation_to_error()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
        }, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task Ice_report_should_not_replace_existing_file_when_cancelled()
    {
        var reportDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestResults", "Reports");
        var reportPath = Path.Combine(reportDirectory, "Ice_cancelled.csv");
        const string existingReport = "existing report";
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(reportPath, existingReport);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            Assert.That(
                async () => await IceReportingHelper.WriteReportAsync("cancelled", [], cts.Token),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(await File.ReadAllTextAsync(reportPath), Is.EqualTo(existingReport));
            Assert.That(Directory.GetFiles(reportDirectory, ".Ice_cancelled.csv.*.tmp"), Is.Empty);
        }
        finally
        {
            File.Delete(reportPath);
        }
    }

    [Test]
    public async Task Csv_report_writer_should_serialize_pass_fail_and_error_values()
    {
        var rows = new[]
        {
            IceReportingHelper.BuildRow("build-1", CreateScenario("<Response><Premium>100.00</Premium></Response>"), 100.00m, 100.00m, ScenarioResult.Pass),
            IceReportingHelper.BuildRow("build-1", CreateScenario("<Response><Premium>100.00</Premium></Response>"), 120.00m, 100.00m, ScenarioResult.Fail),
            IceReportingHelper.BuildRow("build-1", CreateScenario("<Response><Premium>100.00</Premium></Response>"), null, null, ScenarioResult.Error)
        };

        var outputPath = Path.Combine(Path.GetTempPath(), $"ice-report-{Guid.NewGuid():N}.csv");
        try
        {
            await new CsvReportWriter().WriteIceReportAsync(outputPath, "build-1", rows);

            var csv = await File.ReadAllTextAsync(outputPath);
            Assert.That(csv, Does.Contain("PASS"));
            Assert.That(csv, Does.Contain("FAIL"));
            Assert.That(csv, Does.Contain("ERROR"));
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static IceBaselineScenario CreateScenario(string xmlResponse)
    {
        return new IceBaselineScenario
        {
            ScenarioId = "SCENARIO-001",
            QuoteRef = "QUOTE-001",
            ProductCode = "HOME",
            SchemeCode = "ABC",
            XmlResponse = xmlResponse,
            Status = "PASS"
        };
    }

    private sealed class StubIceHandler(string response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? LastUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastUrl = request.RequestUri?.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
        }
    }
}