namespace FuzzyPricingMatcher.Tests.Loader;

public enum LoaderScenarioOutcome
{
    Inserted,
    XmlUpdated,
    TagsUpdated,
    Unchanged,
    Deleted,
    DuplicateScenarioId,
    InvalidCsvRecord,
    InvalidRequestXml,
    MissingSchemeCode,
    MissingPolicyReference,
    RouteNotFound,
    RouteDisabled,
    ApiFailed,
    ResponseValidationFailed,
    DatabaseConsistencyFailed,
    DatabaseFailed
}