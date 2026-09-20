using FuzzyPricingMatcher.Tests.Api;
using FuzzyPricingMatcher.Tests.Configuration;
using FuzzyPricingMatcher.Tests.Data;
using FuzzyPricingMatcher.Tests.Models;
using FuzzyPricingMatcher.Tests.Processing;

namespace FuzzyPricingMatcher.Tests.Loader;
using FuzzyPricingMatcher.Tests.Comparison;

public sealed class ProductionLoaderRepository : ILoaderRepository
{
    private readonly IFuzzyMatcherRepository repository;
    private readonly ITagNormalizer tagNormalizer = new TagNormalizer();

    public ProductionLoaderRepository(IFuzzyMatcherRepository repository) => this.repository = repository;

    public BaselineDatabaseSnapshot GetByScenarioId(string scenarioId)
    {
        var request = repository.GetRequestAsync(scenarioId).GetAwaiter().GetResult();
        var response = repository.GetResponseAsync(scenarioId).GetAwaiter().GetResult();
        return new(
            request is null ? Array.Empty<ExistingRequestRow>() : new[] { new ExistingRequestRow(request.ScenarioId, request.QuoteRef, request.XmlRequest, tagNormalizer.Normalize(request.TestTags)) },
            response is null ? Array.Empty<ExistingResponseRow>() : new[] { new ExistingResponseRow(response.ScenarioId, response.QuoteRef, response.XmlResponse, response.BuildId, response.Status) });
    }

    public IReadOnlyList<string> GetAllScenarioIds() => repository.GetAllRequestScenarioIdsAsync().GetAwaiter().GetResult();

    public void Insert(InsertBaselineScenarioCommand command) => repository.InsertBaselineAsync(new DatabaseInsertCommand(command.ScenarioId, command.QuoteRef, command.RawXml, string.Join(",", command.NormalizedTags), command.RawResponseXml, command.BuildId)).GetAwaiter().GetResult();

    public void Update(UpdateBaselineScenarioCommand command) => repository.UpdateBaselineAsync(new DatabaseUpdateCommand(command.ScenarioId, command.QuoteRef, command.RawXml, string.Join(",", command.NormalizedTags), command.RawResponseXml, command.BuildId)).GetAwaiter().GetResult();

    public void UpdateTags(string scenarioId, IReadOnlyList<string> normalizedTags) => repository.UpdateTagsAsync(new DatabaseTagsUpdateCommand(scenarioId, string.Join(",", normalizedTags))).GetAwaiter().GetResult();

    public void DeleteObsolete(DeleteObsoleteScenarioCommand command) => repository.DeleteObsoleteAsync(command.ScenarioIds).GetAwaiter().GetResult();
}

public sealed class ProductionLoaderApiClient : ILoaderApiClient
{
    private readonly IXmlApiClient apiClient;
    private readonly MatcherConfiguration configuration;
    private readonly IScenarioRouteResolver routeResolver;

    public ProductionLoaderApiClient(IXmlApiClient apiClient, MatcherConfiguration configuration, IScenarioRouteResolver routeResolver)
    {
        this.apiClient = apiClient;
        this.configuration = configuration;
        this.routeResolver = routeResolver;
    }

    public LoaderApiResponse Fetch(LoaderApiRequest request, RouteDefinition route)
    {
        var comparisonRoute = routeResolver.Resolve(route.SchemeCode);
        var result = apiClient.SendAsync(new XmlApiRequest(request.ScenarioId, route.SchemeCode, request.RawXml, configuration.Pipeline.BuildId, comparisonRoute.Endpoint, comparisonRoute.Route, ApiDateResolver.Resolve(configuration.Pipeline.ApiDate))).GetAwaiter().GetResult();
        if (!result.Successful)
            throw new LoaderApiException(result.Error);
        return new LoaderApiResponse(result.ResponseXml);
    }
}