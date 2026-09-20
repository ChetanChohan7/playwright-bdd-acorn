using FuzzyPricingMatcher.Tests.Configuration;

namespace FuzzyPricingMatcher.Tests.Api;

public interface IApiResourceBuilder
{
    ApiResource Build(EndpointSettings endpoint, DateOnly apiDate);
}