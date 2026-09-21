using RestSharp;
using FuzzyPricingMatcher.Tests.Configuration;

namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public interface IExternalServiceClientFactory
{
    RestClient GetClient(string endpointName, EndpointSettings endpoint);
}