using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PricingValidationFramework.Core.Logging;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Tests.Helpers.Reporting;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.Integration.Ice;

[TestFixture]
[Explicit("Requires a configured TB_RESPONSE database, ICE endpoint, credentials, and client certificate.")]
public class IceValidationTests
{
    private readonly IceTestRunLogger logger = new(NullLogger<IceTestRunLogger>.Instance);
    private readonly List<IceValidationReportRow> reportRows = new();
    private readonly List<string> failures = new();
    private IceTestSetup setup = null!;
    private CancellationTokenSource cancellationTokenSource = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        cancellationTokenSource = new CancellationTokenSource();
        setup = IceTestSetup.Create(cancellationTokenSource.Token);
        logger.ExecutionStarted(setup.BuildId);
    }


    [Test]
    public async Task Ice_workload_should_match_all_baselines()
    {
        var scenarios = await setup.BaselineReader
            .GetPassingBaselineScenariosAsync(cancellationTokenSource.Token);

        foreach (var scenario in scenarios)
        {
            try
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                logger.ScenarioStarted(scenario.ScenarioId, scenario.QuoteRef);

                var url = setup.UrlBuilder.Build(setup.IceSettings.IceEndpoint, scenario.QuoteRef);
                logger.RequestPrepared(scenario.ScenarioId, scenario.QuoteRef, url);

                var payload = await setup.ApiClient.GetAsync(url, cancellationTokenSource.Token);
                var iceValue = setup.JsonExtractor.ExtractPremium(payload);
                var baselineValue = setup.XmlExtractor.ExtractBaselineValue(scenario.XmlResponse);
                var passed = iceValue == baselineValue;

                logger.ValidationResult(scenario.ScenarioId, iceValue, baselineValue, passed);

                reportRows.Add(IceReportingHelper.BuildRow(
                    setup.BuildId,
                    scenario,
                    iceValue,
                    baselineValue,
                    passed));

                if (passed)
                {
                    continue;
                }

                failures.Add($"ICE premium mismatch for scenario {scenario.ScenarioId}.");
            }
            catch (Exception ex)
            {
                logger.ExecutionFailed(scenario.ScenarioId, scenario.QuoteRef, ex);
                throw;
            }
        }

        await IceReportingHelper.WriteReportAsync(
            setup.BuildId,
            reportRows,
            cancellationTokenSource.Token,
            logger);

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }


    [OneTimeTearDown]
    public void TearDown()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        setup.Dispose();
        logger.ExecutionCompleted(setup.BuildId);
    }
}
