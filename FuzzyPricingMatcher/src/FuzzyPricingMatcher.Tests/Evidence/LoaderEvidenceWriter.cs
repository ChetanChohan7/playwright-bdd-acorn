using FuzzyPricingMatcher.Tests.Infrastructure;
using FuzzyPricingMatcher.Tests.Loader;

namespace FuzzyPricingMatcher.Tests.Evidence;

public sealed class LoaderEvidenceWriter(EvidencePathBuilder paths, string buildId) : ILoaderEvidenceWriter
{
    public void Save(LoaderEvidence evidence)
    {
        Directory.CreateDirectory(paths.LoaderDirectory(buildId, evidence.ScenarioId));
        File.WriteAllText(paths.LoaderRequestXml(buildId, evidence.ScenarioId), evidence.RawRequestXml);
        if (evidence.RawResponseXml is not null)
            File.WriteAllText(paths.LoaderResponseXml(buildId, evidence.ScenarioId), evidence.RawResponseXml);
        File.WriteAllText(paths.LoaderErrorLog(buildId, evidence.ScenarioId), SafeLogValue.Redact(evidence.Error));
    }
}
