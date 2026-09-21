namespace FuzzyPricingMatcher.Tests.Processing;

public sealed class TagNormalizer
{
    public IReadOnlyList<string> Normalize(string? rawTags) => (rawTags ?? string.Empty)
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}