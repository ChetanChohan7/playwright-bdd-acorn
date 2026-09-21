using FuzzyPricingMatcher.Tests.Configuration;

namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public sealed record ExternalAPIResponse(bool Successful, int? StatusCode, string ResponseXml, string Error, int PhysicalAttempt, TimeSpan Duration, TimeSpan? RetryAfter = null)
{
    public static ExternalAPIResponse Success(int statusCode, string responseXml, int attempt, TimeSpan duration) => new(true, statusCode, responseXml, string.Empty, attempt, duration);
    public static ExternalAPIResponse Failure(int? statusCode, string error, int attempt, TimeSpan duration, TimeSpan? retryAfter = null) => new(false, statusCode, string.Empty, error, attempt, duration, retryAfter);
}

public sealed record ExternalPricingRequestUri(string ResourcePath, string? DateParameterName, string? DateValue)
{
    public bool IsQueryDate => DateParameterName is not null;
}

public sealed record ExternalXmlRequest(string ScenarioId, string SchemeCode, string RawXml, string BuildId, EndpointSettings Endpoint, RouteSettings Route, DateOnly ApiDate);
