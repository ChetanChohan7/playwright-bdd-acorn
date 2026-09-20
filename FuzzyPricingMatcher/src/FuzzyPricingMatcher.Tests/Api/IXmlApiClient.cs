namespace FuzzyPricingMatcher.Tests.Api;

public interface IXmlApiClient
{
    Task<ApiCallResult> SendAsync(XmlApiRequest request, CancellationToken cancellationToken = default);
}