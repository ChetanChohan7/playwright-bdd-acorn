namespace PricingValidationFramework.Core.Models.Database;

public class ScenarioRequest
{
    public string ScenarioId { get; set; } = string.Empty;
    public string QuoteRef { get; set; } = string.Empty;
    public string SchemeCode { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string XmlRequest { get; set; } = string.Empty;
    public string TestTags { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
