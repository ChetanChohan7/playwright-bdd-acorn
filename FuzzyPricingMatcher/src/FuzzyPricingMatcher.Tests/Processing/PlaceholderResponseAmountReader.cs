using System.Globalization;
using System.Xml.Linq;

namespace FuzzyPricingMatcher.Tests.Processing;

public sealed class PlaceholderResponseAmountReader : IResponseAmountReader
{
    public decimal ReadAmount(XDocument validatedResponseDocument)
    {
        var amountElements = validatedResponseDocument.Descendants().Where(element => element.Name.LocalName.Equals("PlaceholderAmount", StringComparison.OrdinalIgnoreCase)).ToList();
        if (amountElements.Count == 0)
            throw new InvalidOperationException("PlaceholderAmount is required.");
        var amountText = amountElements[0].Value.Trim();
        if (amountText.Length == 0)
            throw new InvalidOperationException("PlaceholderAmount cannot be empty.");
        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var comparisonAmount))
            throw new InvalidOperationException("PlaceholderAmount must be a valid invariant decimal.");
        return comparisonAmount;
    }
}