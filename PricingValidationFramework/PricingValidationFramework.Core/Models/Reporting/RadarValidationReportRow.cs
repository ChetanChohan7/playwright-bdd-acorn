namespace PricingValidationFramework.Core.Models.Reporting;

public class RadarValidationReportRow
{
	public string BuildId { get; set; } = string.Empty;
	public string ScenarioId { get; set; } = string.Empty;
	public string QuoteRef { get; set; } = string.Empty;
	public string SchemeCode { get; set; } = string.Empty;
	public string ProductCode { get; set; } = string.Empty;
	public string Result { get; set; } = string.Empty;
	public decimal RadarValue { get; set; }
	public decimal BaselineValue { get; set; }
	public decimal Difference { get; set; }
	public bool FuzzyMatch { get; set; }
}
