namespace PricingValidationFramework.Core.Models.Reporting;

using PricingValidationFramework.Core.Models.Enums;

public class RadarValidationReportRow
{
	public string BuildId { get; set; } = string.Empty;
	public string ScenarioId { get; set; } = string.Empty;
	public string QuoteRef { get; set; } = string.Empty;
	public string SchemeCode { get; set; } = string.Empty;
	public string ProductCode { get; set; } = string.Empty;
	public string RequestXml { get; set; } = string.Empty;
	public string RadarResponseXml { get; set; } = string.Empty;
	public decimal? RadarValue { get; set; }
	public decimal? BaselineValue { get; set; }
	public decimal? Difference { get; set; }
	public decimal MinThreshold { get; set; }
	public decimal MaxThreshold { get; set; }
	public ScenarioResult Result { get; set; }
}
