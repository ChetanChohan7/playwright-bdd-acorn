using ClientAutomationFramework.Core.Matching;

namespace ClientAutomationFramework.Core.Reporting;

/// Aggregates match results from a test run into a plain-text summary.
public sealed class ReportManager
{
    public string BuildSummary(IReadOnlyList<MatchResult> results)
    {
        var passed = results.Count(result => result.Passed);
        var lines = new List<string>
        {
            $"Total: {results.Count}",
            $"Passed: {passed}",
            $"Failed: {results.Count - passed}",
            "Results:"
        };
        lines.AddRange(results.Select(result => $"{result.ScenarioId} | {result.QuoteRef} | {result.Status} | {result.Detail}"));
        return string.Join(Environment.NewLine, lines);
    }

    public void WriteSummary(string path, IReadOnlyList<MatchResult> results)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, BuildSummary(results));
    }
}
