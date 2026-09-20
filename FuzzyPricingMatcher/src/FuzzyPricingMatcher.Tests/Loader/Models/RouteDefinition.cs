namespace FuzzyPricingMatcher.Tests.Loader;

public sealed record RouteDefinition(string SchemeCode, string EndpointName, string ResponseSchemaFile = "", string ResponseProcessorName = "");