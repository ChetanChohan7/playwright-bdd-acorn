using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Tests.Helpers.Reporting;
using PricingValidationFramework.Tests.Helpers.Setup;
using PricingValidationFramework.Tests.Helpers.Validation;

namespace PricingValidationFramework.Tests.Integration.Radar;

[TestFixture]
[Explicit("Requires xml_request and xml_response database access, Radar endpoint access, endpoint credentials, approved XSD files, and Azure DevOps pipeline inputs.")]
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
        decimal? baselineValue = null;
        RadarValidationReportRow row;
        Exception? scenarioError = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            baseline = await setup.BaselineReader
                .GetPassingBaselineByScenarioIdAsync(scenario.ScenarioId, cancellationToken);
            baselineValue = setup.XmlExtractor.ExtractTotalAmount(baseline.XmlResponse);

            row = await setup.ScenarioProcessor.ProcessAsync(
                scenario,
                setup.BuildId,
                baselineValue.Value,
                setup.MinThreshold,
                setup.MaxThreshold,
                setup.RequestTime,
                cancellationToken);

            try
            {
                await PersistResultAsync(setup, baseline, row, cancellationToken);
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
                row = BuildErrorRow(scenario, setup, row.RadarResponseXml, baselineValue);
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
                (exception as HttpRequestException)?.StatusCode is { } statusCode ? (int)statusCode : null,
                (exception as RadarRequestRateLimitException)?.QueueRejected);
            row = BuildErrorRow(scenario, setup, string.Empty, baselineValue);
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
                            reportRows.GetSortedRows(),
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
                $"Radar comparison failed. ScenarioId={row.ScenarioId}, RadarValue={row.RadarValue}, " +
                $"BaselineValue={row.BaselineValue}, Difference={row.Difference}, " +
                $"MinThreshold={row.MinThreshold}, MaxThreshold={row.MaxThreshold}.");
        }

        var errorType = scenarioError?.GetType().Name ?? "RadarProcessingError";
        Assert.Fail($"Radar scenario completed with ERROR. ScenarioId={row.ScenarioId}, ErrorType={errorType}.");
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

        if (row.Result == ScenarioResult.Pass)
        {
            await setup.ResultUpdater.UpdatePassResultAsync(response, cancellationToken);
        }
        else
        {
            await setup.ResultUpdater.UpdateFailResultAsync(response, cancellationToken);
        }
    }

    private static RadarValidationReportRow BuildErrorRow(
        ScenarioRequest scenario,
        RadarTestSetup setup,
        string radarResponseXml,
        decimal? baselineValue)
    {
        return RadarReportingHelper.BuildRow(
            setup.BuildId,
            scenario.ScenarioId,
            scenario.QuoteRef,
            scenario.SchemeCode,
            scenario.ProductCode,
            scenario.XmlRequest,
            radarResponseXml,
            null,
            baselineValue,
            null,
            setup.MinThreshold,
            setup.MaxThreshold,
            ScenarioResult.Error);
    }
}