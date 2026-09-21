using FuzzyPricingMatcher.Tests.Configuration;

namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public sealed record ExternalXmlRequest(string ScenarioId, string SchemeCode, string RawXml, string BuildId, EndpointSettings Endpoint, RouteSettings Route, DateOnly ApiDate);