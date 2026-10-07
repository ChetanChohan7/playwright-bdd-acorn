namespace PricingValidationFramework.Core.Models.Reporting;

using PricingValidationFramework.Core.Models.Enums;

public class IceValidationReportRow
{
	public string BuildId { get; set; } = string.Empty;
	public string ScenarioId { get; set; } = string.Empty;
	public string QuoteRef { get; set; } = string.Empty;
	public string SchemeCode { get; set; } = string.Empty;
	public string ProductCode { get; set; } = string.Empty;
	public decimal? IceValue { get; set; }
	public decimal? BaselineValue { get; set; }
	public ScenarioResult Result { get; set; }
}
