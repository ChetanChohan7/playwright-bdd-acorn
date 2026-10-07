namespace PricingValidationFramework.Core.Extraction;

using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

public class XmlValueExtractor
{
	public decimal ExtractBaselineValue(string xmlResponse)
	{
		var document = ParseResponseXml(xmlResponse);
		var value = document.Descendants()
			.FirstOrDefault(element =>
				string.Equals(element.Name.LocalName, "TotalAmount", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(element.Name.LocalName, "Premium", StringComparison.OrdinalIgnoreCase))?.Value;

		if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var baselineValue))
		{
			throw new InvalidDataException("The baseline XML does not contain a valid TotalAmount or Premium value.");
		}

		return baselineValue;
	}

	private static XDocument ParseResponseXml(string xmlResponse)
	{
		xmlResponse = xmlResponse.TrimStart('\uFEFF').Trim();
		while (xmlResponse.StartsWith('"') || xmlResponse.StartsWith("<?xml version=\\", StringComparison.Ordinal))
		{
			var json = xmlResponse.StartsWith('"') ? xmlResponse : $"\"{xmlResponse}\"";
			xmlResponse = JsonSerializer.Deserialize<string>(json)!;
		}

		return XDocument.Parse(xmlResponse);
	}
}
