namespace FuzzyPricingMatcher.Tests.Api;

public sealed record ApiResource(string ResourcePath, string? DateParameterName, string? DateValue)
{
    public bool IsQueryDate => DateParameterName is not null;
}