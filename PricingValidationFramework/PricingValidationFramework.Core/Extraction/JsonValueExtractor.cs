namespace PricingValidationFramework.Core.Extraction;

using System.Text.Json;

public class JsonValueExtractor
{
    public decimal ExtractPremium(string jsonResponse)
    {
        using var document = JsonDocument.Parse(jsonResponse);

        var hello = FindHelloStatus(document.RootElement);
        if (!IsAcceptedStatus(hello))
        {
            var observedValue = hello.HasValue ? FormatStatus(hello.Value) : "<missing hi field>";
            throw new InvalidDataException(
                $"The ICE response does not contain a wrttenoptions option with hi status \"happy\" or \"sad\" (case-insensitive). Observed hi value: {observedValue}.");
        }

        return FindGrossPremiumAmount(document.RootElement)
            ?? throw new InvalidDataException(
                "The ICE response does not contain a numeric grossPremiumAmountIncTax.amount value.");
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

    private static bool IsAcceptedStatus(JsonElement? status) =>
        status is { ValueKind: JsonValueKind.String } value &&
        (string.Equals(value.GetString(), "happy", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(value.GetString(), "sad", StringComparison.OrdinalIgnoreCase));

    private static string FormatStatus(JsonElement status) =>
        status.ValueKind switch
        {
            JsonValueKind.Object => "<Object>",
            JsonValueKind.Array => "<Array>",
            JsonValueKind.String => JsonSerializer.Serialize(status.GetString()),
            _ => status.GetRawText()
        };

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