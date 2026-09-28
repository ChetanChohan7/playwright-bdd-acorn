namespace PricingValidationFramework.Tests.Helpers.Reporting;

using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Reporting;
using Regex = global::System.Text.RegularExpressions.Regex;

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
        var normalizedRequestXml = NormalizeXml(requestXml);
        var normalizedRadarResponseXml = string.IsNullOrWhiteSpace(radarResponseXml)
            ? string.Empty
            : NormalizeXml(radarResponseXml);

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

    public static async Task WriteReportAsync(
        string buildId,
        IReadOnlyCollection<RadarValidationReportRow> reportRows,
        CancellationToken cancellationToken)
    {
        var reportDirectory = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "TestResults",
            "Reports");
        Directory.CreateDirectory(reportDirectory);
        var reportFileName = $"Radar_{SanitizeBuildId(buildId)}.csv";
        var reportPath = Path.Combine(reportDirectory, reportFileName);
        var temporaryPath = Path.Combine(reportDirectory, $".{reportFileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            await new CsvReportWriter().WriteRadarReportAsync(
                temporaryPath,
                buildId,
                reportRows,
                cancellationToken);
            File.Move(temporaryPath, reportPath, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
            }
            throw;
        }
    }

    private static string SanitizeBuildId(string buildId)
    {
        var safeBuildId = new string((buildId ?? string.Empty)
            .Take(80)
            .Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(safeBuildId) ? "local" : safeBuildId;
    }

    private static string NormalizeXml(string xml)
    {
        return string.IsNullOrWhiteSpace(xml)
            ? string.Empty
            : Regex.Replace(xml, @"\s+", " ").Trim();
    }
}
