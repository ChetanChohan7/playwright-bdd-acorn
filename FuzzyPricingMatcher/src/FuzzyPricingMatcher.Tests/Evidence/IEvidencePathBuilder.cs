namespace FuzzyPricingMatcher.Tests.Evidence;

public interface IEvidencePathBuilder
{
    string BuildRoot(string buildId);
    string LoaderDirectory(string buildId);
    string FailedLoaderScenarioDirectory(string buildId);
    string ScenarioDirectory(string buildId, string scenarioId, string quoteRef);
    string RequestXml(string buildId, string scenarioId, string quoteRef);
    string BaselineXml(string buildId, string scenarioId, string quoteRef);
    string ApiXml(string buildId, string scenarioId, string quoteRef);
    string ScenarioLog(string buildId, string scenarioId, string quoteRef);
}