using RestSharp;
using FuzzyPricingMatcher.Tests.Configuration;

namespace FuzzyPricingMatcher.Tests.Api;

public interface IRestClientFactory
{
    RestClient GetClient(string endpointName, EndpointSettings endpoint);
}