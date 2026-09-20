using FuzzyPricingMatcher.Tests.Configuration;

namespace FuzzyPricingMatcher.Tests.Comparison;

public sealed record ComparisonScenarioRoute(string SchemeCode, string EndpointName, EndpointSettings Endpoint, RouteSettings Route);