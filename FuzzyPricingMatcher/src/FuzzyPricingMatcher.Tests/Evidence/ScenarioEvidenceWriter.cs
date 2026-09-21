using FuzzyPricingMatcher.Tests.Comparison;
using FuzzyPricingMatcher.Tests.Infrastructure;

namespace FuzzyPricingMatcher.Tests.Evidence;

public sealed class ScenarioEvidenceWriter(EvidencePathBuilder paths, string buildId) : IScenarioEvidenceWriter
{
    public void Save(ScenarioEvidence evidence)
    {
        Directory.CreateDirectory(paths.ScenarioDirectory(buildId, evidence.ScenarioId, evidence.QuoteRef));
        File.WriteAllText(paths.RequestXml(buildId, evidence.ScenarioId, evidence.QuoteRef), evidence.RawRequestXml);
        File.WriteAllText(paths.BaselineXml(buildId, evidence.ScenarioId, evidence.QuoteRef), evidence.RawBaselineXml);
        if (evidence.RawApiXml is not null)
            File.WriteAllText(paths.ApiXml(buildId, evidence.ScenarioId, evidence.QuoteRef), evidence.RawApiXml);
        File.WriteAllText(paths.ScenarioLog(buildId, evidence.ScenarioId, evidence.QuoteRef), $"Outcome={evidence.Outcome}{Environment.NewLine}Error={SafeLogValue.Redact(evidence.Error)}{Environment.NewLine}");
    }
}
