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

//remove if statements into utils once we get the xsd's 
	public decimal ExtractTotalAmount(string xmlResponse)
	{
		if (string.IsNullOrWhiteSpace(xmlResponse))
		{
			throw new InvalidDataException("The XML does not contain a TotalAmount value.");
		}

		var document = XDocument.Parse(xmlResponse);
		var totalAmountValues = document.Descendants()
			.Where(element => string.Equals(element.Name.LocalName, "TotalAmount", StringComparison.Ordinal))
			.Select(element => element.Value)
			.ToList();

		if (totalAmountValues.Count == 0)
		{
			throw new InvalidDataException("The XML does not contain a TotalAmount value.");
		}

		if (totalAmountValues.Count > 1)
		{
			throw new InvalidDataException("The XML contains duplicate TotalAmount values.");
		}

		var value = totalAmountValues[0].Trim();
		if (value.Length == 0)
		{
			throw new InvalidDataException("The XML contains an empty TotalAmount value.");
		}

		if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var totalAmount))
		{
			throw new InvalidDataException("The XML contains an invalid TotalAmount value.");
		}

		return totalAmount;
	}
}
