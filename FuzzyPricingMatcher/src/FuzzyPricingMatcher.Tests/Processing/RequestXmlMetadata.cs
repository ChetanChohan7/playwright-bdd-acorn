using System.Xml.Linq;

namespace FuzzyPricingMatcher.Tests.Processing;

public sealed class RequestXmlMetadata
{
    public required string SchemeCode { get; init; }
    public required string QuoteReference { get; init; }
    public required XDocument Document { get; init; }
}