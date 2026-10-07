namespace ClientAutomationFramework.Core.Matching;

/// Compares a newly extracted amount against a baseline amount from the last stored response.
/// If there is no baseline yet, there is nothing to compare against - this passes and the
/// caller's response becomes the baseline for next time.
public sealed class XmlToleranceMatcher(MatchSettings settings)
{
    public MatchResult Evaluate(string scenarioId, string quoteRef, decimal? currentAmount, decimal? baselineAmount)
    {
        if (currentAmount is null)
            return new MatchResult(scenarioId, quoteRef, false, "Response did not contain a readable amount.");

        if (baselineAmount is null)
            return new MatchResult(scenarioId, quoteRef, true, "No prior baseline for this scenario; this response becomes the new baseline.");

        var difference = currentAmount.Value - baselineAmount.Value;
        var withinThreshold = difference >= settings.MinimumThreshold && difference <= settings.MaximumThreshold;
        var detail = $"Baseline={baselineAmount}, Current={currentAmount}, Difference={difference}, Threshold=[{settings.MinimumThreshold}, {settings.MaximumThreshold}].";
        return new MatchResult(scenarioId, quoteRef, withinThreshold, detail);
    }
}
