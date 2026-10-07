using System.Text.Json;

namespace ClientAutomationFramework.Core.Extraction;

public static class JsonValueExtractor
{
    /// Reads underwrittenDataPoints[0].technicalPrice.grossPremiumAmountIncTax.amount.
    /// "technicalPrice" is this API's analog of the XML's "techPrice" - each data point also has
    /// an "underwrittenPrice" sibling with the same field names, so this deliberately navigates
    /// by exact path rather than searching for the amount field anywhere in the document, which
    /// could otherwise match underwrittenPrice's copy instead.
    public static decimal? ExtractPricingComponentAmount(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;
        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("underwrittenDataPoints", out var dataPoints) || dataPoints.ValueKind is not JsonValueKind.Array)
                return null;

            var dataPoint = dataPoints.EnumerateArray().FirstOrDefault();
            if (dataPoint.ValueKind is not JsonValueKind.Object || !dataPoint.TryGetProperty("technicalPrice", out var technicalPrice))
                return null;

            if (!technicalPrice.TryGetProperty("grossPremiumAmountIncTax", out var amountObject) || !amountObject.TryGetProperty("amount", out var amount))
                return null;

            return amount.ValueKind is JsonValueKind.Number ? amount.GetDecimal() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
