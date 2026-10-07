namespace PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;

public class IceUrlBuilder
{
	public string Build(string iceEndpoint, string quoteRef)
	{
		return $"{iceEndpoint.TrimEnd('/')}/{Uri.EscapeDataString(quoteRef)}";
	}
}
