using FuzzyPricingMatcher.Tests.Api;
using FuzzyPricingMatcher.Tests.Loader;
using FuzzyPricingMatcher.Tests.Processing;

namespace FuzzyPricingMatcher.Tests.Validation;

public sealed class SchemaLoaderResponseValidator : ILoaderResponseValidator
{
    private readonly ResponseValidationService validationService;

    public SchemaLoaderResponseValidator(ResponseValidationService validationService) => this.validationService = validationService;

    public void Validate(LoaderApiResponse response, RouteDefinition route)
    {
        if (string.IsNullOrWhiteSpace(route.ResponseSchemaFile))
            throw new LoaderResponseValidationException($"Route '{route.SchemeCode}' has no response schema.");
        if (string.IsNullOrWhiteSpace(route.ResponseProcessorName))
            throw new LoaderResponseValidationException($"Route '{route.SchemeCode}' has no response processor.");
        validationService.ValidateAndReadAmount(new XmlResponseValidationRequest(response.RawXml, string.Empty, string.Empty, "API response", route.ResponseSchemaFile), route.ResponseProcessorName);
    }
}