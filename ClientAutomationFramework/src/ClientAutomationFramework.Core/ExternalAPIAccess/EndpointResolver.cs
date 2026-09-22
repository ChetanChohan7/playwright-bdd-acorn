namespace ClientAutomationFramework.Core.ExternalAPIAccess;

/// Builds the absolute request URL for a scheme. The shared RestClient has no base URL of its
/// own (different schemes live on different hosts), so every client resolves its own URL here
/// before creating a RestRequest.
public static class EndpointResolver
{
    public static string Resolve(SchemeConfig scheme, string? quoteRef = null)
    {
        if (string.IsNullOrWhiteSpace(scheme.BaseUrl))
            throw new InvalidOperationException("A base URL is required to resolve this scheme's endpoint.");

        var resource = quoteRef is null
            ? scheme.ResourceTemplate
            : scheme.ResourceTemplate.Replace("{quoteRef}", Uri.EscapeDataString(quoteRef));

        return $"{scheme.BaseUrl.TrimEnd('/')}/{resource.TrimStart('/')}";
    }
}
