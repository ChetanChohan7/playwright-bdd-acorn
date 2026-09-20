using System.Xml.Linq;

namespace FuzzyPricingMatcher.Tests.Processing;

public interface IResponseAmountReader
{
    decimal ReadAmount(XDocument validatedResponseDocument);
}