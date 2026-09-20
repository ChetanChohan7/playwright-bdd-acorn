namespace FuzzyPricingMatcher.Tests.Comparison;

public interface IScenarioRouteResolver
{
    ComparisonScenarioRoute Resolve(string schemeCode);
}