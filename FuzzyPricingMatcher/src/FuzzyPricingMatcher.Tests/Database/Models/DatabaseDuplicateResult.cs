namespace FuzzyPricingMatcher.Tests.Database;

public sealed record DatabaseDuplicateResult(string TableName, string ScenarioId, int RowCount);