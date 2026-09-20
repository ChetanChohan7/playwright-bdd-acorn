using FuzzyPricingMatcher.Tests.Models;

namespace FuzzyPricingMatcher.Tests.Processing;

public sealed record ExistingBaselineRecord(string XmlFingerprint, IReadOnlyList<string> NormalizedTags);

public sealed record RequestChangeResult(RequestChangeOutcome Outcome, bool RequiresApi);

public interface IRequestChangeDetector
{
    RequestChangeResult Compare(PreparedBaselineScenario current, ExistingBaselineRecord? existing);
}

public sealed class RequestChangeDetector : IRequestChangeDetector
{
    public RequestChangeResult Compare(PreparedBaselineScenario current, ExistingBaselineRecord? existing)
    {
        if (existing is null)
            return new(RequestChangeOutcome.New, true);
        var xmlChanged = !string.Equals(current.XmlFingerprint, existing.XmlFingerprint, StringComparison.Ordinal);
        var tagsChanged = !current.NormalizedTags.SequenceEqual(existing.NormalizedTags, StringComparer.OrdinalIgnoreCase);
        return xmlChanged
            ? new(RequestChangeOutcome.XmlChanged, true)
            : tagsChanged
                ? new(RequestChangeOutcome.TagsChangedOnly, false)
                : new(RequestChangeOutcome.Unchanged, false);
    }
}