namespace PricingValidationFramework.Core.Models.Database;

public class ScenarioResponse
{
    public string ScenarioId { get; set; } = string.Empty;
    public string QuoteRef { get; set; } = string.Empty;
    public string XmlResponse { get; set; } = string.Empty;
    public string BuildId { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string Status { get; set; } = string.Empty;
}
