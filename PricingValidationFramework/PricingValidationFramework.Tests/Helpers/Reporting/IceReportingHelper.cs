namespace PricingValidationFramework.Tests.Helpers.Reporting;

using NUnit.Framework;
using PricingValidationFramework.Core.Logging;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Reporting;

public static class IceReportingHelper
{
    public static IceValidationReportRow BuildRow(
        string buildId,
        IceBaselineScenario scenario,
        decimal iceValue,
        decimal baselineValue,
        bool passed)
    {
        return new IceValidationReportRow
        {
            BuildId = buildId,
            ScenarioId = scenario.ScenarioId,
            QuoteRef = scenario.QuoteRef,
            SchemeCode = scenario.SchemeCode,
            ProductCode = scenario.ProductCode,
            IceValue = iceValue,
            BaselineValue = baselineValue,
            Result = passed ? "PASS" : "FAIL"
        };
    }

    public static async Task WriteReportAsync(
        string buildId,
        IReadOnlyCollection<IceValidationReportRow> reportRows,
        CancellationToken cancellationToken,
        IceTestRunLogger logger)
    {
        var reportPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "TestResults",
            "Reports",
            $"Ice_{buildId}.csv");

        await new CsvReportWriter().WriteIceReportAsync(
            reportPath,
            buildId,
            reportRows,
            cancellationToken);

        logger.ReportGenerated(buildId);
    }
}