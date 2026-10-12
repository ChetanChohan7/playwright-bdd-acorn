namespace PricingValidationFramework.Core.Models.Reporting;

using PricingValidationFramework.Core.Models.Enums;

public sealed record DecimalFieldComparison(
	string FieldKey,
	string ExpectedPath,
	string ActualPath,
	decimal? Expected,
	decimal? Actual,
	decimal? Delta,
	ScenarioResult Result);

public sealed record PricingDecimalValue(string FieldKey, decimal Value, string? XPath = null);

public sealed class PricingDocument
{
	private readonly IReadOnlyDictionary<string, decimal> values;

	public PricingDocument(IEnumerable<PricingDecimalValue> decimalValues)
	{
		ArgumentNullException.ThrowIfNull(decimalValues);
		var fields = decimalValues.ToArray();
		values = fields.ToDictionary(value => value.FieldKey, value => value.Value, StringComparer.Ordinal);
		Paths = fields.ToDictionary(value => value.FieldKey, value => value.XPath ?? value.FieldKey, StringComparer.Ordinal);
	}

	public IReadOnlyDictionary<string, decimal> Values => values;
	public IReadOnlyDictionary<string, string> Paths { get; }
}

public sealed record DecimalExtractionResult(
	bool IsValid,
	PricingDocument? Document,
	IReadOnlyList<string> Errors);

public sealed record PricingBaselineValidationResult(
	bool IsValid,
	string ProfileId,
	PricingDocument? Document = null,
	string? FailureStage = null,
	string? Error = null);

public sealed class PricingComparisonResult
{
	public PricingComparisonResult(
		IReadOnlyList<DecimalFieldComparison> fields,
		string? error = null,
		string? failureStage = null,
		ScenarioResult? result = null,
		string? profileId = null)
	{
		Fields = fields;
		Error = error;
		FailureStage = failureStage;
		ProfileId = profileId ?? string.Empty;
		Result = result ?? (error is not null
			? ScenarioResult.Error
			: fields.Count == 0
				? ScenarioResult.Error
				: fields.Any(comparison => comparison.Result != ScenarioResult.Pass)
					? ScenarioResult.Fail
					: ScenarioResult.Pass);
	}

	public IReadOnlyList<DecimalFieldComparison> Fields { get; }
	public ScenarioResult Result { get; }
	public string? Error { get; }
	public string? FailureStage { get; }
	public string ProfileId { get; }
	public int ComparedFieldCount => Fields.Count;
	public int PassedFieldCount => Fields.Count(comparison => comparison.Result == ScenarioResult.Pass);
	public int FailedFieldCount => Fields.Count(comparison => comparison.Result == ScenarioResult.Fail);
}