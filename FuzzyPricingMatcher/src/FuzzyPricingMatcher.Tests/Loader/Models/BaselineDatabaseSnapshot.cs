namespace FuzzyPricingMatcher.Tests.Loader;

public sealed record BaselineDatabaseSnapshot(IReadOnlyList<ExistingRequestRow> RequestRows, IReadOnlyList<ExistingResponseRow> ResponseRows);