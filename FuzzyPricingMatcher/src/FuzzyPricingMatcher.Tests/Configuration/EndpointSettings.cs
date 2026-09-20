namespace FuzzyPricingMatcher.Tests.Configuration;

public sealed class EndpointSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DatePlacement { get; set; } = "Path";
    public string DateParameterName { get; set; } = "date";
    public string DateFormat { get; set; } = "yyyy-MM-dd";
    public string ContentType { get; set; } = "application/xml";
    public bool Enabled { get; set; }
}