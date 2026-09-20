namespace FuzzyPricingMatcher.Tests.Comparison;

public sealed record ComparisonExecutionResult(ComparisonResult Result, bool DatabaseUpdated, bool EvidencePrepared);