using FuzzyPricingMatcher.Tests.Models;

namespace FuzzyPricingMatcher.Tests.Loader;

public interface ILoaderSynchronizationService
{
    LoaderSynchronizationResult Synchronize(IReadOnlyList<PreparedBaselineScenario> records, string buildId);
}

public interface ILoaderRepository
{
    BaselineDatabaseSnapshot GetByScenarioId(string scenarioId);
    IReadOnlyList<string> GetAllScenarioIds();
    void Insert(InsertBaselineScenarioCommand command);
    void Update(UpdateBaselineScenarioCommand command);
    void UpdateTags(string scenarioId, IReadOnlyList<string> normalizedTags);
    void DeleteObsolete(DeleteObsoleteScenarioCommand command);
}

public interface ILoaderRouteResolver
{
    RouteDefinition Resolve(string schemeCode);
}

public interface ILoaderApiClient
{
    LoaderApiResponse Fetch(LoaderApiRequest request, RouteDefinition route);
}

public interface ILoaderResponseValidator
{
    void Validate(LoaderApiResponse response, RouteDefinition route);
}

public sealed record LoaderEvidence(string ScenarioId, string RawRequestXml, string? RawResponseXml, string Error);

public interface ILoaderEvidenceWriter
{
    void Save(LoaderEvidence evidence);
}

public interface ILoaderSummaryWriter
{
    void Write(string path, LoaderSynchronizationResult result);
}