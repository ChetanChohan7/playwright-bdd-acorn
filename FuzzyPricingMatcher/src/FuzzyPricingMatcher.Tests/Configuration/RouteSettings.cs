namespace FuzzyPricingMatcher.Tests.Configuration;

public sealed class RouteSettings
{
    public string EndpointName { get; set; } = string.Empty;
    public string ResponseSchemaFile { get; set; } = string.Empty;
    public string ResponseProcessorName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Description { get; set; } = string.Empty;
}