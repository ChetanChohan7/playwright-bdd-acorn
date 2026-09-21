namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public sealed record ExternalAPIResponse(bool Successful, int? StatusCode, string ResponseXml, string Error, int PhysicalAttempt, TimeSpan Duration, TimeSpan? RetryAfter = null)
{
    public static ExternalAPIResponse Success(int statusCode, string responseXml, int attempt, TimeSpan duration) => new(true, statusCode, responseXml, string.Empty, attempt, duration);
    public static ExternalAPIResponse Failure(int? statusCode, string error, int attempt, TimeSpan duration, TimeSpan? retryAfter = null) => new(false, statusCode, string.Empty, error, attempt, duration, retryAfter);
}