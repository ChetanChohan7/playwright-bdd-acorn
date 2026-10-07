namespace PricingValidationFramework.Core.Models.Database;

public class IceBaselineScenario
{
    public string ScenarioId { get; set; } = string.Empty;
    public string QuoteRef { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string SchemeCode { get; set; } = string.Empty;
    public string XmlResponse { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastUpdated { get; set; }
}