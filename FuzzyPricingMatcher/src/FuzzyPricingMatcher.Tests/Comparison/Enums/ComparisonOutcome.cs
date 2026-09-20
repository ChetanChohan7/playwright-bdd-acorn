namespace FuzzyPricingMatcher.Tests.Comparison;

public enum ComparisonOutcome
{
    Passed,
    ThresholdFailed,
    ApiFailed,
    ApiValidationFailed,
    BaselineValidationFailed,
    MissingBaseline,
    DatabaseConsistencyFailed,
    RequestInvalid,
    RoutingFailed,
    ExtractionFailed,
    DatabaseFailed,
    Cancelled
}