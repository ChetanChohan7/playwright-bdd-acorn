namespace FuzzyPricingMatcher.Tests.Api;

public sealed record ApiCallResult(bool Successful, int? StatusCode, string ResponseXml, string Error, int PhysicalAttempt, TimeSpan Duration, TimeSpan? RetryAfter = null)
{
    public static ApiCallResult Success(int statusCode, string responseXml, int attempt, TimeSpan duration) => new(true, statusCode, responseXml, string.Empty, attempt, duration);
    public static ApiCallResult Failure(int? statusCode, string error, int attempt, TimeSpan duration, TimeSpan? retryAfter = null) => new(false, statusCode, string.Empty, error, attempt, duration, retryAfter);
}