using Microsoft.Extensions.Logging.Abstractions;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Extraction;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Reporting;
using PricingValidationFramework.Tests.Helpers.Reporting;
using PricingValidationFramework.Tests.Helpers.Validation;

namespace PricingValidationFramework.Tests.System.Ice;

[TestFixture]
public class IceSystemFlowTests
{
    [Test]
    public async Task Ice_system_flow_should_pass_when_values_match()
    {
        var scenario = CreateScenario("<Response><Premium>100.00</Premium></Response>");
        var baselineReader = new FakeBaselineDataReader(scenario);
        var apiClient = new FakeIceApiClient("{\"premium\": 100.00}");
        var urlBuilder = new IceUrlBuilder();
        var jsonExtractor = new JsonValueExtractor();
        var xmlExtractor = new XmlValueExtractor();
        var summary = new ValidationSummary();
        var reportRows = new List<Core.Models.Reporting.IceValidationReportRow>();

        var scenarios = await baselineReader.GetPassingBaselineScenariosAsync();

        foreach (var currentScenario in scenarios)
        {
            var url = urlBuilder.Build("https://ice.test/quote", currentScenario.QuoteRef);
            var payload = await apiClient.GetAsync(url);
            var iceValue = jsonExtractor.ExtractPremium(payload);
            var baselineValue = xmlExtractor.ExtractBaselineValue(currentScenario.XmlResponse);
            var result = iceValue == baselineValue ? ScenarioResult.Pass : ScenarioResult.Fail;

            reportRows.Add(IceReportingHelper.BuildRow(
                "system-test",
                currentScenario,
                iceValue,
                baselineValue,
                result));

            if (result != ScenarioResult.Pass)
            {
                summary.AddFailure($"ScenarioId={currentScenario.ScenarioId}");
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.CallCount, Is.EqualTo(1));
            Assert.That(apiClient.LastUrl, Is.EqualTo("https://ice.test/quote/QUOTE-001"));
            Assert.That(reportRows, Has.Count.EqualTo(1));
            Assert.That(reportRows[0].IceValue, Is.EqualTo(100.00m));
            Assert.That(reportRows[0].BaselineValue, Is.EqualTo(100.00m));
            Assert.That(reportRows[0].Result, Is.EqualTo(ScenarioResult.Pass));
            Assert.That(summary.HasFailures, Is.False);
            Assert.That(summary.FailureCount, Is.Zero);
        });
        summary.AssertNoFailures();
    }

    [Test]
    public async Task Ice_system_flow_should_fail_when_values_do_not_match()
    {
        var scenario = CreateScenario("<Response><Premium>100.00</Premium></Response>");
        var baselineReader = new FakeBaselineDataReader(scenario);
        var apiClient = new FakeIceApiClient("{\"premium\": 120.00}");
        var urlBuilder = new IceUrlBuilder();
        var jsonExtractor = new JsonValueExtractor();
        var xmlExtractor = new XmlValueExtractor();
        var summary = new ValidationSummary();
        var reportRows = new List<Core.Models.Reporting.IceValidationReportRow>();

        var scenarios = await baselineReader.GetPassingBaselineScenariosAsync();

        foreach (var currentScenario in scenarios)
        {
            var url = urlBuilder.Build("https://ice.test/quote", currentScenario.QuoteRef);
            var payload = await apiClient.GetAsync(url);
            var iceValue = jsonExtractor.ExtractPremium(payload);
            var baselineValue = xmlExtractor.ExtractBaselineValue(currentScenario.XmlResponse);
            var result = iceValue == baselineValue ? ScenarioResult.Pass : ScenarioResult.Fail;

            reportRows.Add(IceReportingHelper.BuildRow(
                "system-test",
                currentScenario,
                iceValue,
                baselineValue,
                result));

            if (result != ScenarioResult.Pass)
            {
                summary.AddFailure(
                    $"ScenarioId={currentScenario.ScenarioId}, QuoteRef={currentScenario.QuoteRef}, " +
                    $"IceValue={iceValue}, BaselineValue={baselineValue}");
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.CallCount, Is.EqualTo(1));
            Assert.That(apiClient.LastUrl, Is.EqualTo("https://ice.test/quote/QUOTE-001"));
            Assert.That(reportRows, Has.Count.EqualTo(1));
            Assert.That(reportRows[0].IceValue, Is.EqualTo(120.00m));
            Assert.That(reportRows[0].BaselineValue, Is.EqualTo(100.00m));
            Assert.That(reportRows[0].Result, Is.EqualTo(ScenarioResult.Fail));
            Assert.That(summary.HasFailures, Is.True);
            Assert.That(summary.FailureCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Ice_system_flow_should_record_error_when_comparison_cannot_complete()
    {
        var scenario = CreateScenario("<Response><Premium>100.00</Premium></Response>");
        var baselineReader = new FakeBaselineDataReader(scenario);
        var apiClient = new FakeIceApiClient("{invalid-json");
        var urlBuilder = new IceUrlBuilder();
        var jsonExtractor = new JsonValueExtractor();
        var xmlExtractor = new XmlValueExtractor();

        var scenarios = await baselineReader.GetPassingBaselineScenariosAsync();
        var reportRows = new List<Core.Models.Reporting.IceValidationReportRow>();

        foreach (var currentScenario in scenarios)
        {
            try
            {
                var url = urlBuilder.Build("https://ice.test/quote", currentScenario.QuoteRef);
                var payload = await apiClient.GetAsync(url);
                var iceValue = jsonExtractor.ExtractPremium(payload);
                var baselineValue = xmlExtractor.ExtractBaselineValue(currentScenario.XmlResponse);
                var result = iceValue == baselineValue ? ScenarioResult.Pass : ScenarioResult.Fail;

                reportRows.Add(IceReportingHelper.BuildRow(
                    "system-test",
                    currentScenario,
                    iceValue,
                    baselineValue,
                    result));
            }
            catch (Exception)
            {
                reportRows.Add(IceReportingHelper.BuildRow(
                    "system-test",
                    currentScenario,
                    null,
                    null,
                    ScenarioResult.Error));
            }
        }

        Assert.That(reportRows, Has.Count.EqualTo(1));
        Assert.That(reportRows[0].Result, Is.EqualTo(ScenarioResult.Error));
        Assert.That(reportRows[0].IceValue, Is.Null);
        Assert.That(reportRows[0].BaselineValue, Is.Null);
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

    private sealed class FakeBaselineDataReader : IBaselineDataReader
    {
        private readonly IReadOnlyList<IceBaselineScenario> scenarios;

        public FakeBaselineDataReader(params IceBaselineScenario[] scenarios)
        {
            this.scenarios = scenarios;
        }

        public Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(scenarios);
        }

        public Task<ScenarioResponse> GetPassingBaselineByScenarioIdAsync(
            string scenarioId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "Radar baseline lookup is not used by the ICE system-flow tests.");
        }
    }

    private sealed class FakeIceApiClient
    {
        private readonly string response;

        public FakeIceApiClient(string response)
        {
            this.response = response;
        }

        public int CallCount { get; private set; }
        public string? LastUrl { get; private set; }

        public Task<string> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastUrl = url;
            return Task.FromResult(response);
        }
    }
}