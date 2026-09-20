using FuzzyPricingMatcher.Tests.Configuration;

namespace FuzzyPricingMatcher.Tests.Api;

public sealed record XmlApiRequest(string ScenarioId, string SchemeCode, string RawXml, string BuildId, EndpointSettings Endpoint, RouteSettings Route, DateOnly ApiDate);