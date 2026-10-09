using NUnit.Framework;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Tests.Helpers.Reporting;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.Integration.Ice;

[TestFixture]
[Explicit("Requires configured xml_request/xml_response database tables, ICE endpoint, credentials, and client certificate.")]
[NonParallelizable]
public class IceValidationTests
{
    private readonly List<IceValidationReportRow> reportRows = [];
    private IceTestSetup? setup;
    private Exception? setupFailure;
    private bool cancellationObserved;

    public static IEnumerable<TestCaseData> IceScenarios
    {
        get
        {
            try
            {
                var scenarios = IceTestSetup.DiscoverScenariosAsync(
                    TestContext.CurrentContext.CancellationToken).GetAwaiter().GetResult();
                return CreateScenarioCases(scenarios);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return [DiscoveryFailure($"ICE scenario discovery failed ({exception.GetType().Name}).")];
            }
        }
    }

    internal static IReadOnlyList<TestCaseData> CreateScenarioCases(IEnumerable<IceBaselineScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        var selectedScenarios = scenarios.ToArray();
        if (selectedScenarios.Length == 0)
        {
            return [DiscoveryFailure("No ICE scenarios were returned for the selected workload.")];
        }

        if (selectedScenarios.GroupBy(scenario => scenario.ScenarioId, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            return [DiscoveryFailure("Duplicate ScenarioId values were returned for the ICE workload.")];
        }

        return selectedScenarios.Select(scenario =>
        {
            var safeId = new string(scenario.ScenarioId.Take(64)
                .Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'
                    ? character
                    : '_').ToArray());
            return new TestCaseData(scenario, null)
                .SetName($"Ice_scenario_{(string.IsNullOrWhiteSpace(safeId) ? "unknown" : safeId)}")
                .SetCategory("Ice");
        }).ToArray();
    }

    private static TestCaseData DiscoveryFailure(string safeReason)
    {
        return new TestCaseData(null, safeReason)
            .SetName("Ice_scenario_discovery_should_succeed")
            .SetCategory("IceDiscovery");
    }

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            setup = IceTestSetup.Create();
            setup.Logger.ExecutionStarted(setup.BuildId);
        }
        catch (Exception exception)
        {
            setupFailure = exception;
        }
    }

    [TestCaseSource(nameof(IceScenarios))]
    public async Task Ice_scenario_should_match_baseline(IceBaselineScenario? scenario, string? discoveryFailure)
    {
        if (discoveryFailure is not null)
        {
            throw new AssertionException(discoveryFailure);
        }

        if (scenario is null)
        {
            throw new AssertionException("ICE scenario discovery produced no scenario data.");
        }

        if (setup is null)
        {
            throw new InvalidOperationException($"ICE test setup failed ({setupFailure?.GetType().Name ?? "Unknown"}).");
        }

        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        IceValidationReportRow row;
        Exception? scenarioError = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = setup.UrlBuilder.Build(setup.IceSettings.IceEndpoint, scenario.QuoteRef);
            var payload = await setup.ApiClient.GetAsync(url, cancellationToken);
            var iceValue = setup.JsonExtractor.ExtractPremium(payload);
            var baselineValue = setup.XmlExtractor.ExtractBaselineValue(scenario.XmlResponse);
            row = IceReportingHelper.BuildRow(
                setup.BuildId,
                scenario,
                iceValue,
                baselineValue,
                iceValue == baselineValue ? ScenarioResult.Pass : ScenarioResult.Fail);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationObserved = true;
            setup.Logger.Cancellation(
                "ICE scenario cancelled. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}.",
                scenario.ScenarioId,
                scenario.QuoteRef);
            throw;
        }
        catch (Exception exception)
        {
            scenarioError = exception;
            setup.Logger.ExecutionFailed(scenario.ScenarioId, scenario.QuoteRef, exception);
            row = IceReportingHelper.BuildRow(setup.BuildId, scenario, null, null, ScenarioResult.Error);
        }

        RecordScenarioResult(row, scenarioError);
    }

    internal void RecordScenarioResult(IceValidationReportRow row, Exception? scenarioError = null)
    {
        reportRows.Add(row);
        if (row.Result == ScenarioResult.Pass)
        {
            return;
        }

        if (row.Result == ScenarioResult.Fail)
        {
            Assert.Fail(
                $"ICE comparison failed. ScenarioId={row.ScenarioId}, QuoteRef={row.QuoteRef}, " +
                $"ProductCode={row.ProductCode}, SchemeCode={row.SchemeCode}, " +
                $"IceValue={row.IceValue}, BaselineValue={row.BaselineValue}.");
        }

        Assert.Fail(
            $"ICE scenario completed with ERROR. ScenarioId={row.ScenarioId}, " +
            $"ErrorType={scenarioError?.GetType().Name ?? "IceProcessingError"}, Error={scenarioError?.Message}.");
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        try
        {
            if (setup is not null)
            {
                setup.Logger.ExecutionCompleted(setup.BuildId);
            }

            if (reportRows.Count > 0 || cancellationObserved)
            {
                var buildId = setup?.BuildId ?? reportRows.FirstOrDefault()?.BuildId
                    ?? Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local";
                await IceReportingHelper.WriteReportAsync(buildId, reportRows, CancellationToken.None);
            }
        }
        finally
        {
            setup?.Dispose();
        }
    }
}
