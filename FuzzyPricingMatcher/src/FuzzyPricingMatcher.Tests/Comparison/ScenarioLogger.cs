using NLog;
using NUnit.Framework;
using FuzzyPricingMatcher.Tests.Infrastructure;

namespace FuzzyPricingMatcher.Tests.Comparison;

public sealed class NLogScenarioLogger : IScenarioLogger
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public void Outcome(ComparisonResult result) => Logger.Info("Scenario outcome ScenarioId={ScenarioId} QuoteRef={QuoteRef} Outcome={Outcome} Passed={Passed} ApiValue={ApiValue} BaselineValue={BaselineValue} Difference={Difference} Error={Error}", result.ScenarioId, result.QuoteRef, result.Outcome, result.Passed, result.ApiValue, result.BaselineValue, result.Difference, SafeLogValue.Redact(result.Error));

    public void QuoteMismatch(string scenarioId, string extractedQuoteRef, string storedQuoteRef) => Logger.Warn("QuoteRef mismatch ScenarioId={ScenarioId} ExtractedQuoteRef={ExtractedQuoteRef} StoredQuoteRef={StoredQuoteRef}", scenarioId, extractedQuoteRef, storedQuoteRef);
}

public sealed class NUnitScenarioOutputWriter
{
    public void Write(ComparisonResult result, string buildId, string schemeCode, string endpointName, decimal minimumThreshold, decimal maximumThreshold, string? evidencePath)
    {
        TestContext.Progress.WriteLine($"BuildId={buildId} ScenarioId={result.ScenarioId} QuoteRef={result.QuoteRef} SchemeCode={schemeCode} EndpointName={endpointName} ApiValue={result.ApiValue} BaselineValue={result.BaselineValue} Difference={result.Difference} MinimumThreshold={minimumThreshold} MaximumThreshold={maximumThreshold} Outcome={result.Outcome} Passed={result.Passed} FailureCategory={result.Error} Evidence={evidencePath ?? string.Empty}");
    }
}