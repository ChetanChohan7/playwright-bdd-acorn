namespace FuzzyPricingMatcher.Tests.Loader;

public sealed record LoaderSynchronizationResult(IReadOnlyList<LoaderScenarioResult> ScenarioResults, string DeletionSkippedReason = "")
{
    public bool Successful => ScenarioResults.All(result => result.Successful) && string.IsNullOrWhiteSpace(DeletionSkippedReason);
    public bool HasErrors => ScenarioResults.Any(result => !result.Successful);
}