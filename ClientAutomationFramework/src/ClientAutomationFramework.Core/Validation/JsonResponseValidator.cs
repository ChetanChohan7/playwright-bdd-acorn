using System.Text.Json;

namespace ClientAutomationFramework.Core.Validation;

public sealed record JsonValidationResult(bool Valid, string Error);

/// Confirms the response is well-formed JSON and contains the given required property
/// somewhere in the document - a lightweight substitute for a real JSON schema, since none was
/// available for the quote API when this was written.
public sealed class JsonResponseValidator(string requiredPropertyName)
{
    public JsonValidationResult Validate(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return new JsonValidationResult(false, "Response body is empty.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawJson);
        }
        catch (JsonException exception)
        {
            return new JsonValidationResult(false, $"Malformed JSON: {exception.Message}");
        }

        using (document)
        {
            return HasProperty(document.RootElement, requiredPropertyName)
                ? new JsonValidationResult(true, string.Empty)
                : new JsonValidationResult(false, $"Required property '{requiredPropertyName}' was not found.");
        }
    }

    private static bool HasProperty(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (HasProperty(property.Value, propertyName))
                        return true;
                }
                return false;
            case JsonValueKind.Array:
                return element.EnumerateArray().Any(item => HasProperty(item, propertyName));
            default:
                return false;
        }
    }
}
