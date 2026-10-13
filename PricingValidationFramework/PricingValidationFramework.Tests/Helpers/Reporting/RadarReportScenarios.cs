using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace PricingValidationFramework.Tests.Helpers.Reporting;

/// Reads a Radar_<BuildId>.csv report back, for ICE's "this Radar run" selection.
internal static class RadarReportScenarios
{
    /// The distinct ScenarioIds whose ScenarioResult is PASS, in report order. The report repeats a
    /// scenario on one row per compared field; setup/discovery rows are never PASS.
    public static async Task<IReadOnlyList<string>> ReadPassedScenarioIdsAsync(string reportPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(reportPath))
        {
            throw new FileNotFoundException($"Radar report '{reportPath}' was not found.", reportPath);
        }

        using var reader = new StreamReader(reportPath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { ExceptionMessagesContainRawData = false });
        if (!await csv.ReadAsync() || !csv.ReadHeader() ||
            !csv.HeaderRecord!.Contains("ScenarioId") || !csv.HeaderRecord.Contains("ScenarioResult"))
        {
            throw new InvalidDataException($"Radar report '{reportPath}' has no ScenarioId and ScenarioResult columns.");
        }

        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scenarioId = csv.GetField("ScenarioId");
            if (string.Equals(csv.GetField("ScenarioResult"), "PASS", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(scenarioId) && seen.Add(scenarioId))
            {
                ids.Add(scenarioId);
            }
        }

        return ids;
    }
}
