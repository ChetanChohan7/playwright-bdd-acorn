using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Tests.Helpers.Reporting;
using PricingValidationFramework.Tests.Helpers.Setup;
using PricingValidationFramework.Tests.Helpers.Validation;

namespace PricingValidationFramework.Tests.Integration.Radar;

[TestFixture]
[Explicit("Requires XML database access, Radar endpoint credentials, pipeline inputs, and pricing XPath mappings.")]
[Parallelizable(ParallelScope.Children)]
public class RadarValidationTests
{
    private const string MissingBaselineStage = "MissingBaseline";

    private readonly RadarReportCollection reportRows = new();
    private RadarTestSetup? setup;
    private Exception? setupFailure;
    private int cancellationObserved;
    private readonly NLog.Logger diagnostics = NLog.LogManager.GetLogger("PricingValidationFramework.Tests.Integration.Radar");

    public static IEnumerable<TestCaseData> RadarScenarios
    {
        get
        {
            try
            {
                var scenarios = RadarTestSetup.DiscoverScenariosAsync(
                    includeLocalSettings: true, TestContext.CurrentContext.CancellationToken).GetAwaiter().GetResult();
                return RadarScenarioTestCases.Create(scenarios);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return [RadarScenarioTestCases.DiscoveryFailure(
                    $"Radar scenario discovery failed ({exception.GetType().Name}).")];
            }
        }
    }

    [OneTimeSetUp]
    public void SetUp()
    {
        NLog.GlobalDiagnosticsContext.Set("RadarBuildId", Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local");
        diagnostics.Info("Radar setup starting.");
        try
        {
            setup = RadarTestSetup.Create(includeLocalSettings: true);
            setup.Logger.ExecutionStarted(setup.BuildId);
        }
        catch (Exception exception)
        {
            setupFailure = exception;
            diagnostics.Error("Radar setup failed ({0}). {1}", exception.GetType().Name,
                exception is InvalidDataException or FileNotFoundException ? exception.Message : "Check runtime inputs and configuration.");
        }
    }

    [TestCaseSource(nameof(RadarScenarios))]
    public async Task Radar_scenario_should_validate(ScenarioRequest? scenario, string? discoveryFailure)
    {
        if (discoveryFailure is not null)
        {
            reportRows.Add(new RadarValidationReportRow { ScenarioId = "RadarDiscovery", Result = ScenarioResult.Error,
                FailureStage = "Discovery", Error = discoveryFailure });
            diagnostics.Error(discoveryFailure);
            throw new AssertionException(discoveryFailure);
        }

        if (scenario is null)
        {
            throw new AssertionException("Radar scenario discovery produced no scenario data.");
        }

        if (setup is null)
        {
            reportRows.Add(new RadarValidationReportRow { ScenarioId = scenario.ScenarioId, QuoteRef = scenario.QuoteRef,
                ProductCode = scenario.ProductCode, SchemeCode = scenario.SchemeCode, RequestXml = scenario.XmlRequest,
                Result = ScenarioResult.Error, FailureStage = "Setup",
                Error = $"Radar setup failed ({setupFailure?.GetType().Name ?? "Unknown"}). Check the run log." });
            throw new InvalidOperationException($"Radar test setup failed ({setupFailure?.GetType().Name ?? "Unknown"}).");
        }

        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        ScenarioResponse? baseline = null;
        RadarValidationReportRow row;
        Exception? scenarioError = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            baseline = await setup.BaselineReader
                .GetBaselineByScenarioIdAsync(scenario.ScenarioId, cancellationToken);

            // Radar compares only. Baselines are created by `DataLoader seed` (the data-loader
            // pipeline's Seed baselines stage), so a scenario without one is an error, not a PASS.
            if (baseline is null)
            {
                var missing = $"No baseline in [Xml].[Response] for ScenarioId '{scenario.ScenarioId}'. Run DataLoader seed first.";
                setup.Logger.ExecutionFailed(
                    scenario.ScenarioId,
                    scenario.QuoteRef,
                    scenario.ProductCode,
                    scenario.SchemeCode,
                    null,
                    MissingBaselineStage,
                    new InvalidOperationException(missing),
                    safeDetail: missing);
                row = BuildErrorRow(scenario, setup, string.Empty, string.Empty, MissingBaselineStage, missing);
            }
            else
            {
                (row, scenarioError) = await CompareAndPersistAsync(setup, scenario, baseline, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Interlocked.Exchange(ref cancellationObserved, 1);
            setup.Logger.Cancellation(
                "Radar scenario cancelled. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}, ProductCode={ProductCode}, SchemeCode={SchemeCode}, ExecutionStage={ExecutionStage}.",
                scenario.ScenarioId,
                scenario.QuoteRef,
                scenario.ProductCode,
                scenario.SchemeCode,
                "ScenarioExecution");
            throw;
        }
        catch (Exception exception)
        {
            scenarioError = exception;
            setup.Logger.ExecutionFailed(
                scenario.ScenarioId,
                scenario.QuoteRef,
                scenario.ProductCode,
                scenario.SchemeCode,
                exception is RadarRequestRateLimitException rateLimitException ? rateLimitException.EndpointName : null,
                exception switch
                {
                    RadarRequestRateLimitException => "RateLimitPermit",
                    HttpRequestException { StatusCode: global::System.Net.HttpStatusCode.TooManyRequests } => "Http429",
                    _ => "BaselineLoad"
                },
                exception,
                (exception as HttpRequestException)?.StatusCode is { } statusCode ? (int)statusCode : null);
            row = BuildErrorRow(scenario, setup, baseline?.XmlResponse ?? string.Empty, string.Empty, "ScenarioExecution", exception.Message);
        }

        reportRows.Add(row);
        AssertScenarioResult(row, scenarioError);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        try
        {
            setup?.Logger.ExecutionCompleted(setup.BuildId);
            if (reportRows.Count == 0)
            {
                reportRows.Add(new RadarValidationReportRow { ScenarioId = "RadarRun", Result = ScenarioResult.Error,
                    FailureStage = "Execution", Error = Volatile.Read(ref cancellationObserved) != 0
                        ? "Radar run was cancelled before any scenario completed." : "No Radar scenarios completed." });
            }
            await RadarReportingHelper.WriteReportAsync(
                setup?.BuildId ?? Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local",
                reportRows.ReadSortedRows(), CancellationToken.None);
            diagnostics.Info("Radar report written.");
        }
        catch (Exception exception)
        {
            diagnostics.Error("Radar report generation failed ({0}).", exception.GetType().Name);
            throw;
        }
        finally
        {
            reportRows.Dispose();
            setup?.Dispose();
            NLog.LogManager.Flush();
        }
    }

    private static async Task<(RadarValidationReportRow Row, Exception? Error)> CompareAndPersistAsync(
        RadarTestSetup setup,
        ScenarioRequest scenario,
        ScenarioResponse baseline,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(baseline.Status, "PASS", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"No passing baseline was found for ScenarioId '{scenario.ScenarioId}' (Status is '{baseline.Status}').");
        }

        var run = await setup.PricingService.RunAsync(
            scenario,
            baseline.XmlResponse,
            setup.RequestTime,
            setup.MinThreshold,
            setup.MaxThreshold,
            setup.ThresholdType,
            cancellationToken);
        var row = RadarReportingHelper.BuildComparisonRow(
            setup.BuildId,
            scenario.ScenarioId,
            scenario.QuoteRef,
            scenario.SchemeCode,
            scenario.ProductCode,
            scenario.XmlRequest,
            baseline.XmlResponse,
            run.RadarResponseXml,
            setup.MinThreshold,
            setup.MaxThreshold,
            run.Comparison);
        if (row.Result != ScenarioResult.Pass)
        {
            setup.Logger.ExecutionFailed(
                scenario.ScenarioId,
                scenario.QuoteRef,
                scenario.ProductCode,
                scenario.SchemeCode,
                run.RouteId,
                row.FailureStage ?? "PricingComparison",
                new InvalidOperationException(row.Error ?? $"Pricing comparison returned {row.Result}."),
                safeDetail: row.Error);
        }

        if (row.Result == ScenarioResult.Error || string.IsNullOrWhiteSpace(row.RadarResponseXml))
        {
            return (row, null);
        }

        try
        {
            await PersistResultAsync(setup, baseline, row, cancellationToken);
            return (row, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            setup.Logger.ExecutionFailed(
                scenario.ScenarioId,
                scenario.QuoteRef,
                scenario.ProductCode,
                scenario.SchemeCode,
                null,
                "ResultPersistence",
                exception);
            return (BuildErrorRow(scenario, setup, baseline.XmlResponse, row.RadarResponseXml, "ResultPersistence", exception.Message), exception);
        }
    }

    private static void AssertScenarioResult(RadarValidationReportRow row, Exception? scenarioError)
    {
        if (row.Result == ScenarioResult.Pass)
        {
            return;
        }

        if (row.Result == ScenarioResult.Fail)
        {
            Assert.Fail(
                $"Radar comparison failed. ScenarioId={row.ScenarioId}, FailedFields={row.FieldComparisons.Count(field => field.Result == ScenarioResult.Fail)}, " +
                $"Details={string.Join(" | ", row.FieldComparisons.Where(field => field.Result == ScenarioResult.Fail).Select(field => $"{field.FieldKey}: expected={field.Expected}, actual={field.Actual}, delta={field.Delta}"))}.");
        }

        var errorType = scenarioError?.GetType().Name ?? row.FailureStage ?? "RadarProcessingError";
        Assert.Fail($"Radar scenario completed with ERROR. ScenarioId={row.ScenarioId}, ErrorType={errorType}, Error={row.Error}.");
    }

    private static async Task PersistResultAsync(
        RadarTestSetup setup,
        ScenarioResponse baseline,
        RadarValidationReportRow row,
        CancellationToken cancellationToken)
    {
        var response = new ScenarioResponse
        {
            ScenarioId = baseline.ScenarioId,
            QuoteRef = baseline.QuoteRef,
            XmlResponse = row.RadarResponseXml,
            CreatedDate = baseline.CreatedDate,
            LastUpdated = baseline.LastUpdated,
            Status = row.Result == ScenarioResult.Pass ? "PASS" : "FAIL"
        };

        await setup.ResultUpdater.UpdateResultAsync(response, cancellationToken);
    }

    private static RadarValidationReportRow BuildErrorRow(
        ScenarioRequest scenario,
        RadarTestSetup setup,
        string baselineXml,
        string radarResponseXml,
        string failureStage,
        string error)
    {
        return RadarReportingHelper.BuildComparisonRow(
            setup.BuildId,
            scenario.ScenarioId,
            scenario.QuoteRef,
            scenario.SchemeCode,
            scenario.ProductCode,
            scenario.XmlRequest,
            baselineXml,
            radarResponseXml,
            setup.MinThreshold,
            setup.MaxThreshold,
            new PricingComparisonResult(Array.Empty<DecimalFieldComparison>(), error, failureStage));
    }
}