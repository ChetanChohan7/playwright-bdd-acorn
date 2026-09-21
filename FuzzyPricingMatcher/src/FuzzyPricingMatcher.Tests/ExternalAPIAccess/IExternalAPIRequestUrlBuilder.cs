using FuzzyPricingMatcher.Tests.Configuration;

namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public interface IExternalAPIRequestUrlBuilder
{
    ExternalPricingRequestUri BuildRequestUri(EndpointSettings endpoint, DateOnly apiDate);
}