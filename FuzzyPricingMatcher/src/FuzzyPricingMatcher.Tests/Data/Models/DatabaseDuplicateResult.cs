namespace FuzzyPricingMatcher.Tests.Data;

public sealed record DatabaseDuplicateResult(string TableName, string ScenarioId, int RowCount);