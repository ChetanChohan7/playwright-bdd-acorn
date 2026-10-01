namespace PricingValidationFramework.Tests.Helpers.Reporting;

using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Reporting;

public static class RadarReportingHelper
{
    public static RadarValidationReportRow BuildRow(
        string buildId,
        string scenarioId,
        string quoteRef,
        string schemeCode,
        string productCode,
        string requestXml,
        string radarResponseXml,
        decimal? radarValue,
        decimal? baselineValue,
        decimal? difference,
        decimal minThreshold,
        decimal maxThreshold,
        ScenarioResult result)
    {
        var normalizedRequestXml = CsvReportWriter.NormalizeXml(requestXml);
        var normalizedRadarResponseXml = string.IsNullOrWhiteSpace(radarResponseXml)
            ? string.Empty
            : CsvReportWriter.NormalizeXml(radarResponseXml);

        if (result == ScenarioResult.Error)
        {
            radarValue = string.IsNullOrWhiteSpace(radarResponseXml) ? null : radarValue;
        }

        return new RadarValidationReportRow
        {
            BuildId = buildId,
            ScenarioId = scenarioId,
            QuoteRef = quoteRef,
            SchemeCode = schemeCode,
            ProductCode = productCode,
            RequestXml = normalizedRequestXml,
            RadarResponseXml = normalizedRadarResponseXml,
            RadarValue = radarValue,
            BaselineValue = baselineValue,
            Difference = difference,
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
        var reportDirectory = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "TestResults",
            "Reports");
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, $"Radar_{SanitizeBuildId(buildId)}.csv");

        return AtomicReportWriter.WriteAsync(
            reportPath,
            temporaryPath => new CsvReportWriter().WriteRadarReportAsync(temporaryPath, buildId, reportRows, cancellationToken));
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
