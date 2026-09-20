namespace FuzzyPricingMatcher.Tests.Loader;

public sealed class LoaderSummaryWriter : ILoaderSummaryWriter
{
    public void Write(string path, LoaderSynchronizationResult result)
        => Write(path, result, new LoaderSummaryContext("LOCAL", string.Empty, DateTimeOffset.UtcNow));

    public void Write(string path, LoaderSynchronizationResult result, LoaderSummaryContext context)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var lines = result.ScenarioResults.Select(item => string.Join(" | ", item.ScenarioId, item.QuoteRef, item.Successful, item.Outcome, item.ApiCalled, item.RequestTableAction, item.ResponseTableAction, item.Error));
        var results = result.ScenarioResults;
        var summary = new[]
        {
            $"Build ID: {context.BuildId}",
            $"Run timestamp: {context.RunTimestamp:O}",
            $"CSV path: {context.CsvPath}",
            $"Total scenarios: {results.Count}",
            $"Successful scenarios: {results.Count(item => item.Successful)}",
            $"Failed scenarios: {results.Count(item => !item.Successful)}",
            $"Inserted: {results.Count(item => item.Outcome == LoaderScenarioOutcome.Inserted)}",
            $"XML updated: {results.Count(item => item.Outcome == LoaderScenarioOutcome.XmlUpdated)}",
            $"Tags updated: {results.Count(item => item.Outcome == LoaderScenarioOutcome.TagsUpdated)}",
            $"Unchanged: {results.Count(item => item.Outcome == LoaderScenarioOutcome.Unchanged)}",
            $"Deleted: {results.Count(item => item.Outcome == LoaderScenarioOutcome.Deleted)}",
            $"Deletion performed: {results.Any(item => item.Outcome == LoaderScenarioOutcome.Deleted)}",
            $"Deletion skipped: {(!string.IsNullOrWhiteSpace(result.DeletionSkippedReason))}",
            $"Deletion skip reason: {result.DeletionSkippedReason}",
            "Results:"
        };
        File.WriteAllLines(path, summary.Concat(lines));
    }
}

public sealed record LoaderSummaryContext(string BuildId, string CsvPath, DateTimeOffset RunTimestamp);