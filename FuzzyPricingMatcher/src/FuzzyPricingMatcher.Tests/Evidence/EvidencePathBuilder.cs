namespace FuzzyPricingMatcher.Tests.Evidence;

public sealed class EvidencePathBuilder(string root)
{
    private readonly string root = Path.GetFullPath(root);

    public string BuildRoot(string buildId) => Path.Combine(root, $"Build-{SafeFileName.Convert(buildId)}");

    public string ScenarioDirectory(string buildId, string scenarioId, string quoteRef) =>
        Path.Combine(BuildRoot(buildId), SafeFileName.Convert(scenarioId), SafeFileName.Convert(quoteRef));

    public string RequestXml(string buildId, string scenarioId, string quoteRef) =>
        Path.Combine(ScenarioDirectory(buildId, scenarioId, quoteRef), "request.xml");

    public string BaselineXml(string buildId, string scenarioId, string quoteRef) =>
        Path.Combine(ScenarioDirectory(buildId, scenarioId, quoteRef), "baseline.xml");

    public string ApiXml(string buildId, string scenarioId, string quoteRef) =>
        Path.Combine(ScenarioDirectory(buildId, scenarioId, quoteRef), "api.xml");

    public string ScenarioLog(string buildId, string scenarioId, string quoteRef) =>
        Path.Combine(ScenarioDirectory(buildId, scenarioId, quoteRef), "scenario.log");

    public string LoaderDirectory(string buildId, string scenarioId) =>
        Path.Combine(BuildRoot(buildId), "Loader", SafeFileName.Convert(scenarioId));

    public string LoaderRequestXml(string buildId, string scenarioId) =>
        Path.Combine(LoaderDirectory(buildId, scenarioId), "request.xml");

    public string LoaderResponseXml(string buildId, string scenarioId) =>
        Path.Combine(LoaderDirectory(buildId, scenarioId), "response.xml");

    public string LoaderErrorLog(string buildId, string scenarioId) =>
        Path.Combine(LoaderDirectory(buildId, scenarioId), "error.log");
}
