namespace FuzzyPricingMatcher.Tests.Processing;

public interface IRequestXmlMetadataReader
{
    RequestXmlMetadata Read(string rawXml);
}