namespace ClientAutomationFramework.Core.ExternalAPIAccess;

/// Configuration for one external API scheme (an endpoint this framework calls).
/// ResourceTemplate may contain "{quoteRef}", replaced per-request by EndpointResolver.
public sealed class SchemeConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ResourceTemplate { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/xml";
    public int TimeoutSeconds { get; set; } = 60;
}
