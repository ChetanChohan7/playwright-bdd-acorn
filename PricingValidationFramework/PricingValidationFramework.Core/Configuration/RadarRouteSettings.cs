namespace PricingValidationFramework.Core.Configuration;

public class RadarRouteSettings
{
    public string ProductCode { get; set; } = string.Empty;
    public List<string> SchemeCodes { get; set; } = new();
    public string EndpointName { get; set; } = string.Empty;
    public string RouteKey { get; set; } = string.Empty;
}
