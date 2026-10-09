using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Tests.Helpers.Reporting;
using PricingValidationFramework.Tests.Helpers.Setup;
using PricingValidationFramework.Tests.Helpers.Validation;

namespace PricingValidationFramework.Tests.Integration.Radar;

[TestFixture]
[Explicit("Requires XML database access, Radar endpoint credentials, pipeline inputs, and route-mapped pricing XSDs.")]
[Parallelizable(ParallelScope.Children)]
public class RadarValidationTests
{
    private readonly RadarReportCollection reportRows = new();
    private RadarTestSetup? setup;
    private Exception? setupFailure;
    private int cancellationObserved;

    public static IEnumerable<TestCaseData> RadarScenarios
    {
        get
        {
            try
            {
                var scenarios = RadarTestSetup.DiscoverScenariosAsync(
                    TestContext.CurrentContext.CancellationToken).GetAwaiter().GetResult();
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
        try
        {
            setup = RadarTestSetup.Create();
            setup.Logger.ExecutionStarted(setup.BuildId);
        }
        catch (Exception exception)
        {
            setupFailure = exception;
        }
    }

    [TestCaseSource(nameof(RadarScenarios))]
    public async Task Radar_scenario_should_validate(ScenarioRequest? scenario, string? discoveryFailure)
    {
        if (discoveryFailure is not null)
        {
            throw new AssertionException(discoveryFailure);
        }

        if (scenario is null)
        {
            throw new AssertionException("Radar scenario discovery produced no scenario data.");
        }

        if (setup is null)
        {
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
            if (baseline is not null && !string.Equals(baseline.Status, "PASS", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"No passing baseline was found for ScenarioId '{scenario.ScenarioId}' (Status is '{baseline.Status}').");
            }

            // A new scenario has no xml_response row yet: Radar's response becomes its baseline
            // and is passed without the fuzzy comparison. Otherwise compare against the baseline.
			var run = baseline is null
                ? await setup.PricingService.CreateBaselineAsync(scenario, setup.RequestTime, cancellationToken)
                : await setup.PricingService.RunAsync(
                    scenario,
                    baseline.XmlResponse,
                    setup.RequestTime,
                    setup.MinThreshold,
                    setup.MaxThreshold,
                    cancellationToken);
			row = RadarReportingHelper.BuildComparisonRow(
				setup.BuildId,
				scenario.ScenarioId,
				scenario.QuoteRef,
				scenario.SchemeCode,
				scenario.ProductCode,
				scenario.XmlRequest,
				baseline?.XmlResponse ?? string.Empty,
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

			if (row.Result != ScenarioResult.Error && !string.IsNullOrWhiteSpace(row.RadarResponseXml))
            {
                try
                {
                    if (baseline is null)
                    {
                        await setup.ResultUpdater.InsertBaselineAsync(new ScenarioResponse
                        {
                            ScenarioId = scenario.ScenarioId,
                            QuoteRef = scenario.QuoteRef,
                            XmlResponse = run.RadarResponseXml,
                            BuildId = setup.BuildId,
                            Status = "PASS"
                        }, cancellationToken);
                    }
                    else
                    {
                        await PersistResultAsync(setup, baseline, row, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
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
                        null,
                        "ResultPersistence",
                        exception);
                    row = BuildErrorRow(scenario, setup, baseline?.XmlResponse ?? string.Empty, row.RadarResponseXml, "ResultPersistence", exception.Message);
                }
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
            if (setup is not null)
            {
                setup.Logger.ExecutionCompleted(setup.BuildId);
                if (reportRows.Count > 0 || Volatile.Read(ref cancellationObserved) != 0)
                {
                    try
                    {
                        await RadarReportingHelper.WriteReportAsync(
                            setup.BuildId,
                            reportRows.ReadSortedRows(),
                            CancellationToken.None);
                    }
                    catch (Exception exception)
                    {
                        setup.Logger.ReportGenerationFailed(exception);
                        throw;
                    }
                }
            }
        }
        finally
        {
            reportRows.Dispose();
            setup?.Dispose();
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
            BuildId = setup.BuildId,
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