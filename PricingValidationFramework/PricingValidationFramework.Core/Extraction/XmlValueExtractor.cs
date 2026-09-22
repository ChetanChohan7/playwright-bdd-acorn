namespace PricingValidationFramework.Core.Extraction;

using System.Globalization;
using System.Xml.Linq;

public class XmlValueExtractor
{
	public decimal ExtractBaselineValue(string xmlResponse)
	{
		var document = XDocument.Parse(xmlResponse);
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
}
