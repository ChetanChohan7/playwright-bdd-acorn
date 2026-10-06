namespace PricingValidationFramework.Tests.Helpers.Reporting;

using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Reporting;

public static class RadarReportingHelper
{
    public static RadarValidationReportRow BuildComparisonRow(
        string buildId,
        string scenarioId,
        string quoteRef,
        string schemeCode,
        string productCode,
        string requestXml,
        string baselineXml,
        string radarResponseXml,
        decimal minDelta,
        decimal maxDelta,
        PricingComparisonResult comparison)
    {
        return BuildRow(
            buildId,
            scenarioId,
            quoteRef,
            schemeCode,
            productCode,
            requestXml,
            radarResponseXml,
            minDelta,
            maxDelta,
            comparison.Result,
            comparison.SchemaProfile,
            baselineXml,
            comparison.Fields,
            comparison.FailureStage,
            comparison.Error);
    }

    private static RadarValidationReportRow BuildRow(
        string buildId,
        string scenarioId,
        string quoteRef,
        string schemeCode,
        string productCode,
        string requestXml,
        string radarResponseXml,
        decimal minThreshold,
        decimal maxThreshold,
        ScenarioResult result,
        string schemaProfile,
        string baselineXml,
        IReadOnlyList<DecimalFieldComparison> fieldComparisons,
        string? failureStage,
        string? error)
    {
        var normalizedRequestXml = CsvReportWriter.NormalizeXml(requestXml);
        var normalizedRadarResponseXml = string.IsNullOrWhiteSpace(radarResponseXml)
            ? string.Empty
            : CsvReportWriter.NormalizeXml(radarResponseXml);

        return new RadarValidationReportRow
        {
            BuildId = buildId,
            ScenarioId = scenarioId,
            QuoteRef = quoteRef,
            SchemeCode = schemeCode,
            ProductCode = productCode,
            SchemaProfile = schemaProfile,
            RequestXml = normalizedRequestXml,
            BaselineXml = string.IsNullOrWhiteSpace(baselineXml) ? string.Empty : CsvReportWriter.NormalizeXml(baselineXml),
            RadarResponseXml = normalizedRadarResponseXml,
            FieldComparisons = fieldComparisons,
            FailureStage = failureStage,
            Error = error,
            MinThreshold = minThreshold,
            MaxThreshold = maxThreshold,
            Result = result
        };
    }

    public static Task WriteReportAsync(
        string buildId,
        IReadOnlyCollection<RadarValidationReportRow> reportRows,
        CancellationToken cancellationToken)
    {
        return WriteReportAsync(buildId,
            reportRows.OrderBy(row => row.ScenarioId, StringComparer.Ordinal), cancellationToken);
    }

    public static Task WriteReportAsync(
        string buildId,
        IEnumerable<RadarValidationReportRow> sortedRows,
        CancellationToken cancellationToken)
    {
        var reportDirectory = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "TestResults",
            "Reports");
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, $"Radar_{SanitizeBuildId(buildId)}.csv");

        return AtomicReportWriter.WriteAsync(
            reportPath,
            temporaryPath => new CsvReportWriter().WriteRadarReportRowsAsync(temporaryPath, buildId, sortedRows, cancellationToken));
    }

    private static string SanitizeBuildId(string buildId)
    {
        var safeBuildId = new string((buildId ?? string.Empty)
            .Take(80)
            .Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(safeBuildId) ? "local" : safeBuildId;
    }
}
