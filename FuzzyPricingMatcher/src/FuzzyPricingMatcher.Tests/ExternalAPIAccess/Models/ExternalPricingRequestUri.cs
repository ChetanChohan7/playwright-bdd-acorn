namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public sealed record ExternalPricingRequestUri(string ResourcePath, string? DateParameterName, string? DateValue)
{
    public bool IsQueryDate => DateParameterName is not null;
}