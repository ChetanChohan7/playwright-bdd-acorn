namespace FuzzyPricingMatcher.Tests.Processing;

public interface ITagNormalizer
{
    IReadOnlyList<string> Normalize(string? rawTags);
}