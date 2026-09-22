using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Extraction;
using PricingValidationFramework.Core.Logging;
using PricingValidationFramework.Core.Models.Common;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Reporting;
using PricingValidationFramework.Core.Validation;

namespace PricingValidationFramework.Tests.Integration.Ice;

[TestFixture]
[Explicit("Requires a configured TB_RESPONSE database, ICE endpoint, credentials, and client certificate.")]
public class IceValidationTests
{
    [Test]
    public async Task Passing_baselines_match_current_ice_premiums()
    {
        var cancellationToken = CancellationToken.None;
        var configuration = new ConfigurationBuilder()
            .SetBasePath(TestContext.CurrentContext.TestDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var databaseSettings = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>()
            ?? throw new InvalidOperationException("DatabaseSettings is missing.");
        var iceSettings = configuration.GetSection("IceSettings").Get<IceSettings>()
            ?? throw new InvalidOperationException("IceSettings is missing.");

        var pipelineSettings = new PipelineSettings
        {
            BuildId = Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local",
            MinThreshold = ReadDecimalEnvironment("ICE_MIN_THRESHOLD", -0.01m),
            MaxThreshold = ReadDecimalEnvironment("ICE_MAX_THRESHOLD", 0.01m)
        };
        new PipelineInputValidator().Validate(pipelineSettings);

        var logger = new IceTestRunLogger(NullLogger<IceTestRunLogger>.Instance);
        var baselineReader = new BaselineDataReader(new SqlConnectionFactory(databaseSettings));
        logger.DatabaseActivity("Loading passing ICE baseline scenarios from TB_RESPONSE.");
        var scenarios = await baselineReader.GetPassingBaselineScenariosAsync(cancellationToken);
        using var apiClient = new IceApiClient(iceSettings, NullLogger<IceApiClient>.Instance);
        var urlBuilder = new IceUrlBuilder();
        var jsonExtractor = new JsonValueExtractor();
        var xmlExtractor = new XmlValueExtractor();
        var reportRows = new List<IceValidationReportRow>(scenarios.Count);
        foreach (var scenario in scenarios)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = urlBuilder.Build(iceSettings.IceEndpoint, scenario.QuoteRef);
            logger.ApiActivity("Calling ICE for scenario {ScenarioId}.", scenario.ScenarioId);
            var payload = await apiClient.GetAsync(url, cancellationToken);
            var iceValue = jsonExtractor.ExtractPremium(payload);
            var baselineValue = xmlExtractor.ExtractBaselineValue(scenario.XmlResponse);
            var difference = iceValue - baselineValue;
            var passed = difference >= pipelineSettings.MinThreshold && difference <= pipelineSettings.MaxThreshold;
            logger.ResponseSummary("ICE response received for scenario {ScenarioId}.", scenario.ScenarioId);
            logger.AssertionOutcome("Scenario {ScenarioId} ICE result: {Result}.", scenario.ScenarioId, passed ? "PASS" : "FAIL");
            reportRows.Add(new IceValidationReportRow
            {
                BuildId = pipelineSettings.BuildId,
                ScenarioId = scenario.ScenarioId,
                QuoteRef = scenario.QuoteRef,
                SchemeCode = scenario.SchemeCode,
                ProductCode = scenario.ProductCode,
                Result = passed ? "PASS" : "FAIL",
                IceValue = iceValue,
                BaselineValue = baselineValue,
                Difference = difference
            });
            Assert.That(passed, Is.True, $"ICE premium mismatch for scenario {scenario.ScenarioId}.");
        }

        var reportPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestResults", "Reports", $"Ice_{pipelineSettings.BuildId}.csv");
        await new CsvReportWriter().WriteIceReportAsync(reportPath, pipelineSettings.BuildId, reportRows, cancellationToken);
    }

    private static decimal ReadDecimalEnvironment(string name, decimal defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return decimal.TryParse(value, out var result) ? result : defaultValue;
    }
}
