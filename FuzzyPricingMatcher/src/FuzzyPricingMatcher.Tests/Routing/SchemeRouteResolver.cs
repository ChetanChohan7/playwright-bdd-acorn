using FuzzyPricingMatcher.Tests.Comparison;
using FuzzyPricingMatcher.Tests.Configuration;
using FuzzyPricingMatcher.Tests.Loader;

namespace FuzzyPricingMatcher.Tests.Routing;

public sealed class SchemeRouteResolver : ILoaderRouteResolver, IScenarioRouteResolver
{
    private readonly MatcherConfiguration configuration;
    private readonly IntegrationConfigurationValidator validator;

    public SchemeRouteResolver(MatcherConfiguration configuration, IntegrationConfigurationValidator validator)
    {
        this.configuration = configuration;
        this.validator = validator;
    }

    public RouteDefinition Resolve(string schemeCode)
    {
        var route = validator.ValidateRoute(schemeCode);
        return new RouteDefinition(schemeCode, route.EndpointName, route.ResponseSchemaFile, route.ResponseProcessorName);
    }

    ComparisonScenarioRoute IScenarioRouteResolver.Resolve(string schemeCode)
    {
        var route = validator.ValidateRoute(schemeCode);
        var endpoint = configuration.Endpoints[route.EndpointName];
        return new ComparisonScenarioRoute(schemeCode, route.EndpointName, endpoint, route);
    }
}