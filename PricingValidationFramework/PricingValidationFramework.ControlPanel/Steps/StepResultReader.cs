namespace PricingValidationFramework.ControlPanel.Steps;

using System.Text.Json;
using System.Text.RegularExpressions;

/// Turns a finished step's summary file or test output into a few lines for the page.
public static partial class StepResultReader
{
    // The one-line totals ("Failed!  - Failed: 2, Passed: 8, Skipped: 0, Total: 10, ...")...
    [GeneratedRegex(@"(?:Passed|Failed)!\s+-\s+Failed:\s+(?<failed>\d+),\s+Passed:\s+(?<passed>\d+),\s+Skipped:\s+(?<skipped>\d+),\s+Total:\s+(?<total>\d+)")]
    private static partial Regex TestTotals();

    // ...or, with the detailed console logger, one count per line after "Total tests: 10".
    [GeneratedRegex(@"^\s*(?<name>Total tests|Passed|Failed|Skipped):\s+(?<count>\d+)\s*$")]
    private static partial Regex TestCountLine();

    public static IReadOnlyList<string> Summarize(StepKind kind, ProcessSpec spec, IReadOnlyList<string> output)
    {
        if (kind is StepKind.Radar or StepKind.Ice)
        {
            var totals = output.Select(line => TestTotals().Match(line)).LastOrDefault(match => match.Success);
            if (totals is not null)
            {
                return [$"Scenarios: {totals.Groups["total"].Value}. Passed {totals.Groups["passed"].Value}, failed or errored {totals.Groups["failed"].Value}."];
            }

            var start = output.ToList().FindLastIndex(line => TestCountLine().Match(line) is { Success: true } match && match.Groups["name"].Value == "Total tests");
            if (start < 0)
            {
                return ["No test results were reported. See the output for build or setup errors."];
            }

            var counts = output.Skip(start).Select(line => TestCountLine().Match(line)).Where(match => match.Success)
                .GroupBy(match => match.Groups["name"].Value).ToDictionary(group => group.Key, group => group.First().Groups["count"].Value);
            return [$"Scenarios: {counts["Total tests"]}. Passed {counts.GetValueOrDefault("Passed", "0")}, failed or errored {counts.GetValueOrDefault("Failed", "0")}."];
        }

        var summaryFile = spec.Outputs.FirstOrDefault()?.Path;
        if (summaryFile is null || !File.Exists(summaryFile))
        {
            return ["No summary was written. See the output for what went wrong."];
        }

        using var json = JsonDocument.Parse(File.ReadAllText(summaryFile));
        var root = json.RootElement;
        var lines = kind switch
        {
            StepKind.ValidateCsv => new List<string> { $"File: {Text(root, "RequestFile")}", $"Requests read: {Number(root, "InputRequests")}." },
            StepKind.Import => [$"File: {Text(root, "RequestFile")}",
                $"Requests read {Number(root, "InputRequests")}: inserted {Number(root, "InsertedRequests")}, updated {Number(root, "UpdatedRequests")}, unchanged {Number(root, "SkippedRequests")}."],
            StepKind.Seed => [$"Request time {Text(root, "RequestTime")}. Scenarios without a baseline: {Number(root, "ScenariosWithoutBaseline")}.",
                $"Seeded {Number(root, "Seeded")}, already existed {Number(root, "AlreadyExisted")}, failed {Number(root, "Failed")}, not run {Number(root, "NotRun")}."],
            StepKind.SetStatus => [$"Set to {Text(root, "Status")}, recorded as {Text(root, "ChangedBy")}.",
                $"Updated {Number(root, "Updated")}, already {Text(root, "Status")} {Number(root, "AlreadySet")}, not found {Number(root, "NotFound")}."],
            _ => []
        };

        // Problems: the first few, each with its row or scenario.
        if (root.TryGetProperty("Issues", out var issues) && issues.ValueKind == JsonValueKind.Array)
        {
            var all = issues.EnumerateArray().ToArray();
            lines.AddRange(all.Take(10).Select(issue => issue.ValueKind == JsonValueKind.String
                ? $"Problem: {issue.GetString()}"
                : $"Problem{RowLabel(issue)}: {Text(issue, "Reason")}"));
            if (all.Length > 10)
            {
                lines.Add($"... and {all.Length - 10} more problem(s) in the summary file.");
            }
        }

        if (kind == StepKind.Seed && root.TryGetProperty("Results", out var results))
        {
            foreach (var failed in results.EnumerateArray().Where(result => Text(result, "Outcome") == "Failed").Take(10))
            {
                lines.Add($"Not seeded: {Text(failed, "ScenarioId")} - {Text(failed, "Stage")}: {Text(failed, "Error")}");
            }
        }

        if (kind == StepKind.SetStatus && root.TryGetProperty("Results", out var changes))
        {
            foreach (var missing in changes.EnumerateArray().Where(change => Text(change, "Outcome") == "NotFound").Take(10))
            {
                lines.Add($"Not found: {Text(missing, "ScenarioId")}");
            }
        }

        return lines;
    }

    private static string RowLabel(JsonElement issue)
    {
        var record = issue.TryGetProperty("RecordNumber", out var number) && number.TryGetInt32(out var value) && value > 0 ? $" in row {value}" : string.Empty;
        var scenario = Text(issue, "ScenarioId");
        return record + (scenario.Length > 0 ? $" ({scenario})" : string.Empty);
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetRawText() : "?";
}
