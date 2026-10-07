namespace PricingValidationFramework.Core.Extraction;

using System.Text.Json;

public class JsonValueExtractor
{
    public decimal ExtractPremium(string jsonResponse)
    {
        using var document = JsonDocument.Parse(jsonResponse);

        return FindGrossPremiumAmount(document.RootElement)
            ?? throw new InvalidDataException(
                "The ICE response does not contain a numeric grossPremiumAmountIncTax.amount value.");
    }

    private static decimal? FindGrossPremiumAmount(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(
                        property.Name,
                        "grossPremiumAmountIncTax",
                        StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.Object &&
                    property.Value.TryGetProperty("amount", out var amount) &&
                    amount.ValueKind == JsonValueKind.Number &&
                    amount.TryGetDecimal(out var value))
                {
                    return value;
                }

                var nestedValue = FindGrossPremiumAmount(property.Value);
                if (nestedValue.HasValue)
                {
                    return nestedValue;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nestedValue = FindGrossPremiumAmount(item);
                if (nestedValue.HasValue)
                {
                    return nestedValue;
                }
            }
        }

        return null;
    }
}