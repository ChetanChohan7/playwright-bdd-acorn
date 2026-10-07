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

public sealed record PricingDecimalValue(string XmlPath, decimal Value);

public sealed class PricingDocument
{
	private readonly IReadOnlyDictionary<string, decimal> values;

	public PricingDocument(IEnumerable<PricingDecimalValue> decimalValues)
	{
		ArgumentNullException.ThrowIfNull(decimalValues);
		values = decimalValues.ToDictionary(value => value.XmlPath, value => value.Value, StringComparer.Ordinal);
	}

	public IReadOnlyDictionary<string, decimal> Values => values;
}

public sealed record XsdDecimalExtractionResult(
	bool IsValid,
	PricingDocument? Document,
	IReadOnlyList<string> Errors);

public sealed record PricingBaselineValidationResult(
	bool IsValid,
	string SchemaProfile,
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
		string? schemaProfile = null)
	{
		Fields = fields;
		Error = error;
		FailureStage = failureStage;
		SchemaProfile = schemaProfile ?? string.Empty;
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
	public string SchemaProfile { get; }
	public int ComparedFieldCount => Fields.Count;
	public int PassedFieldCount => Fields.Count(comparison => comparison.Result == ScenarioResult.Pass);
	public int FailedFieldCount => Fields.Count(comparison => comparison.Result == ScenarioResult.Fail);
}