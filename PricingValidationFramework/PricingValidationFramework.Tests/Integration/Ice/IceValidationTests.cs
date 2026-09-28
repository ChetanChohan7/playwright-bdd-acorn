using NUnit.Framework;
using PricingValidationFramework.Core.Logging;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Tests.Helpers.Reporting;
using PricingValidationFramework.Tests.Helpers.Setup;
using PricingValidationFramework.Tests.Helpers.Validation;

namespace PricingValidationFramework.Tests.Integration.Ice;

[TestFixture]
[Explicit("Requires a configured TB_RESPONSE database, ICE endpoint, credentials, and client certificate.")]
public class IceValidationTests
{
    private IceTestRunLogger logger = null!;
    private IceTestSetup setup = null!;
    private CancellationToken runCancellationToken;

    [OneTimeSetUp]
    public void SetUp()
    {
        runCancellationToken = TestContext.CurrentContext.CancellationToken;
        setup = IceTestSetup.Create(runCancellationToken);
        logger = setup.Logger;
        logger.ExecutionStarted(setup.BuildId);
    }


    [Test]
    public async Task Ice_workload_should_match_all_baselines()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        var reportRows = new List<IceValidationReportRow>();
        var summary = new ValidationSummary();
        var scenarios = await setup.BaselineReader
            .GetPassingBaselineScenariosAsync(cancellationToken);

        foreach (var scenario in scenarios)
        {
            var iceValue = default(decimal);
            var baselineValue = default(decimal);
            var reportRowAdded = false;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var url = setup.UrlBuilder.Build(setup.IceSettings.IceEndpoint, scenario.QuoteRef);

                var payload = await setup.ApiClient.GetAsync(url, cancellationToken);
                iceValue = setup.JsonExtractor.ExtractPremium(payload);
                baselineValue = setup.XmlExtractor.ExtractBaselineValue(scenario.XmlResponse);
                var result = iceValue == baselineValue ? ScenarioResult.Pass : ScenarioResult.Fail;

                reportRows.Add(IceReportingHelper.BuildRow(
                    setup.BuildId,
                    scenario,
                    iceValue,
                    baselineValue,
                    result));
                reportRowAdded = true;

                if (result == ScenarioResult.Pass)
                {
                    continue;
                }

                summary.AddFailure(
                    $"ScenarioId={scenario.ScenarioId}, QuoteRef={scenario.QuoteRef}, " +
                    $"ProductCode={scenario.ProductCode}, SchemeCode={scenario.SchemeCode}, " +
                    $"IceValue={iceValue}, BaselineValue={baselineValue}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.Cancellation(
                    "ICE workload cancelled. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}.",
                    scenario.ScenarioId,
                    scenario.QuoteRef);
                throw;
            }
            catch (Exception ex)
            {
                logger.ExecutionFailed(scenario.ScenarioId, scenario.QuoteRef, ex);

                if (!reportRowAdded)
                {
                    reportRows.Add(IceReportingHelper.BuildRow(
                        setup.BuildId,
                        scenario,
                        null,
                        null,
                        ScenarioResult.Error));
                }

                summary.AddFailure(
                    $"ScenarioId={scenario.ScenarioId}, QuoteRef={scenario.QuoteRef}, " +
                    $"ProductCode={scenario.ProductCode}, SchemeCode={scenario.SchemeCode}, " +
                    $"IceValue={iceValue}, BaselineValue={baselineValue}, " +
                    $"Error={ex.Message}");
            }
        }

        await IceReportingHelper.WriteReportAsync(
            setup.BuildId,
            reportRows,
            cancellationToken);

        summary.AssertNoFailures();
    }


    [OneTimeTearDown]
    public void TearDown()
    {
        logger.ExecutionCompleted(setup.BuildId);
        setup.Dispose();
    }
}
