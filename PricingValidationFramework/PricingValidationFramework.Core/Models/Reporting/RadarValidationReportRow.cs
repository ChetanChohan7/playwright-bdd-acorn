namespace PricingValidationFramework.Core.Models.Reporting;

using PricingValidationFramework.Core.Models.Enums;

public class RadarValidationReportRow
{
	public string BuildId { get; set; } = string.Empty;
	public string ScenarioId { get; set; } = string.Empty;
	public string QuoteRef { get; set; } = string.Empty;
	public string SchemeCode { get; set; } = string.Empty;
	public string ProductCode { get; set; } = string.Empty;
	public string SchemaProfile { get; set; } = string.Empty;
	public string RequestXml { get; set; } = string.Empty;
	public string BaselineXml { get; set; } = string.Empty;
	public string RadarResponseXml { get; set; } = string.Empty;
	public IReadOnlyList<DecimalFieldComparison> FieldComparisons { get; set; } = Array.Empty<DecimalFieldComparison>();
	public string? FailureStage { get; set; }
	public string? Error { get; set; }
	public decimal MinThreshold { get; set; }
	public decimal MaxThreshold { get; set; }
	public ScenarioResult Result { get; set; }
}
