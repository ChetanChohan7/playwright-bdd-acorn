namespace PricingValidationFramework.Core.Extraction;

using System.Text.Json;

public class JsonValueExtractor
{
	public decimal ExtractPremium(string jsonResponse)
	{
		using var document = JsonDocument.Parse(jsonResponse);
		return FindDecimal(document.RootElement, "premium")
			?? throw new InvalidDataException("The ICE response does not contain a Premium value.");
	}

	private static decimal? FindDecimal(JsonElement element, string propertyName)
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			foreach (var property in element.EnumerateObject())
			{
				if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
					property.Value.TryGetDecimal(out var value))
				{
					return value;
				}

				var nestedValue = FindDecimal(property.Value, propertyName);
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
				var nestedValue = FindDecimal(item, propertyName);
				if (nestedValue.HasValue)
				{
					return nestedValue;
				}
			}
		}

		return null;
	}
}
