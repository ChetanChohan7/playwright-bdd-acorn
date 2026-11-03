namespace PricingValidationFramework.Core.Extraction;

using System.Text.Json;

public class JsonValueExtractor
{
    /// grossPremiumAmountIncTax.amount, once the response's wrttenoptions "hi" status is "happy".
    public decimal ExtractPremium(string jsonResponse)
    {
        using var document = JsonDocument.Parse(jsonResponse);

        return Premium(document.RootElement);
    }

    /// The total of the "amount" fields in the first "taxes" list in the response (searched in document
    /// order, any depth, any case): { "taxes": [ { "amount": 100, "code": "IPT" }, { "amount": 20 } ] } gives
    /// 120. Every other field is ignored. Only that first "taxes" is used; later ones never are.
    public decimal ExtractTaxes(string jsonResponse)
    {
        using var document = JsonDocument.Parse(jsonResponse);

        return Taxes(document.RootElement);
    }

    /// The premium without tax: ExtractPremium minus ExtractTaxes, from one read of the response. The
    /// "happy" status check applies, as for ExtractPremium.
    public decimal ExtractPremiumExcludingTaxes(string jsonResponse)
    {
        using var document = JsonDocument.Parse(jsonResponse);

        return Premium(document.RootElement) - Taxes(document.RootElement);
    }

    private static decimal Premium(JsonElement root)
    {
        var hello = FindHelloStatus(root);
        if (hello is not { ValueKind: JsonValueKind.String } status ||
            !string.Equals(status.GetString(), "happy", StringComparison.OrdinalIgnoreCase))
        {
            var observedValue = hello.HasValue ? FormatStatus(hello.Value) : "<missing hi field>";
            throw new InvalidDataException(
                $"The ICE response does not contain a wrttenoptions option with hi status \"happy\" (case-insensitive). Observed hi value: {observedValue}.");
        }

        return FindAmount(root, "grossPremiumAmountIncTax")
            ?? throw new InvalidDataException(
                "The ICE response does not contain a numeric grossPremiumAmountIncTax.amount value.");
    }

    private static decimal Taxes(JsonElement root)
    {
        var taxes = FindFirstProperty(root, "taxes")
            ?? throw new InvalidDataException("The ICE response does not contain a taxes list.");
        if (taxes.ValueKind != JsonValueKind.Array || taxes.GetArrayLength() == 0)
        {
            throw new InvalidDataException("The ICE response's first taxes value is not a list with at least one entry.");
        }

        var total = 0m;
        var position = 0;
        foreach (var tax in taxes.EnumerateArray())
        {
            position++;
            if (tax.ValueKind != JsonValueKind.Object ||
                !tax.TryGetProperty("amount", out var amount) ||
                amount.ValueKind != JsonValueKind.Number ||
                !amount.TryGetDecimal(out var value))
            {
                throw new InvalidDataException($"Entry {position} of the ICE response's taxes list has no numeric amount.");
            }

            total += value;
        }

        return total;
    }

    private static JsonElement? FindHelloStatus(JsonElement element)
    {
        var pending = new Stack<JsonElement>();
        pending.Push(element);

        while (pending.TryPop(out var current))
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in current.EnumerateObject())
                {
                    pending.Push(property.Value);
                    if (!property.NameEquals("wrttenoptions") ||
                        property.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var option in property.Value.EnumerateArray())
                    {
                        if (option.ValueKind == JsonValueKind.Object &&
                            option.TryGetProperty("hi", out var status))
                        {
                            return status;
                        }
                    }
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in current.EnumerateArray())
                {
                    pending.Push(item);
                }
            }
        }

        return null;
    }

    private static string FormatStatus(JsonElement status) =>
        status.ValueKind switch
        {
            JsonValueKind.Object => "<Object>",
            JsonValueKind.Array => "<Array>",
            JsonValueKind.String => JsonSerializer.Serialize(status.GetString()),
            _ => status.GetRawText()
        };

    /// The value of the first property with this name (any case), searching objects and arrays in document order.
    private static JsonElement? FindFirstProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }

                var nested = FindFirstProperty(property.Value, propertyName);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindFirstProperty(item, propertyName);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    /// Searches every object and array for a property with this name (any case) whose value is an
    /// object with a numeric "amount", and returns the first such amount.
    private static decimal? FindAmount(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(
                        property.Name,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.Object &&
                    property.Value.TryGetProperty("amount", out var amount) &&
                    amount.ValueKind == JsonValueKind.Number &&
                    amount.TryGetDecimal(out var value))
                {
                    return value;
                }

                var nestedValue = FindAmount(property.Value, propertyName);
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
                var nestedValue = FindAmount(item, propertyName);
                if (nestedValue.HasValue)
                {
                    return nestedValue;
                }
            }
        }

        return null;
    }
}
