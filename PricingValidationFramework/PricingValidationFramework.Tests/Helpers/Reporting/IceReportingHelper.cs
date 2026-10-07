namespace PricingValidationFramework.Tests.Helpers.Reporting;

using NUnit.Framework;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Reporting;

public static class IceReportingHelper
{
    public static IceValidationReportRow BuildRow(
        string buildId,
        IceBaselineScenario scenario,
        decimal? iceValue,
        decimal? baselineValue,
        ScenarioResult result)
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
            Result = result
        };
    }

    public static Task WriteReportAsync(
        string buildId,
        IReadOnlyCollection<IceValidationReportRow> reportRows,
        CancellationToken cancellationToken)
    {
        var reportPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "TestResults",
            "Reports",
            $"Ice_{buildId}.csv");

        return AtomicReportWriter.WriteAsync(
            reportPath,
            temporaryPath => new CsvReportWriter().WriteIceReportAsync(temporaryPath, buildId, reportRows, cancellationToken));
    }
}