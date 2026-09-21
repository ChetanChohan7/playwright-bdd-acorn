namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public interface IExternalXmlServiceClient
{
    Task<ExternalAPIResponse> SendXmlRequestAsync(ExternalXmlRequest request, CancellationToken cancellationToken = default);
}