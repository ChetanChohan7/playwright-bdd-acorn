namespace PricingValidationFramework.Core.Extraction;

using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml;
using System.Xml.XPath;
using PricingValidationFramework.Core.Models.Reporting;

public class XmlValueExtractor
{
	public DecimalExtractionResult Extract(
		string xmlResponse,
		IReadOnlyDictionary<string, string> fields,
		IReadOnlySet<string>? sumFields = null)
	{
		var values = new List<PricingDecimalValue>();
		var errors = new List<string>();
		XDocument document;
		try
		{
			document = ParseResponseXml(xmlResponse);
		}
		catch (Exception exception) when (exception is XmlException or JsonException)
		{
			return new DecimalExtractionResult(false, null, ["Response XML could not be parsed."]);
		}
		foreach (var field in fields)
		{
			try
			{
				var shouldSum = sumFields?.Contains(field.Key) == true;
				var value = ReadValue(document, field.Key, field.Value, shouldSum);
				var reportPath = shouldSum ? $"SUM({field.Value})" : field.Value;
				values.Add(new PricingDecimalValue(field.Key, value, reportPath));
			}
			catch (InvalidDataException exception)
			{
				errors.Add(exception.Message);
			}
		}
		if (fields.Count == 0)
		{
			errors.Add("No XPath fields were configured.");
		}
		return new DecimalExtractionResult(errors.Count == 0,
			errors.Count == 0 ? new PricingDocument(values) : null, errors);
	}

	public decimal ExtractValue(string xmlResponse, string fieldName, string xpath, bool sumMatches = false)
	{
		IReadOnlySet<string>? sumFields = sumMatches ? new HashSet<string>(StringComparer.Ordinal) { fieldName } : null;
		var result = Extract(xmlResponse, new Dictionary<string, string> { [fieldName] = xpath }, sumFields);
		return result.IsValid
			? result.Document!.Values[fieldName]
			: throw new InvalidDataException(string.Join(" | ", result.Errors));
	}

	private static decimal ReadValue(XDocument document, string fieldName, string xpath, bool sumMatches)
	{
		var description = $"Field '{fieldName}' at XPath '{xpath}'";
		XPathNodeIterator nodes;
		try
		{
			nodes = document.CreateNavigator().Select(xpath);
		}
		catch (XPathException exception)
		{
			throw new InvalidDataException($"{description} is not a supported node-selection XPath.", exception);
		}
		var count = 0;
		var total = 0m;
		while (nodes.MoveNext())
		{
			if (!sumMatches && count > 0)
			{
				throw new InvalidDataException($"{description} matched more than one value.");
			}

			var node = nodes.Current!;
			if (node.NodeType is not (XPathNodeType.Element or XPathNodeType.Attribute))
			{
				throw new InvalidDataException($"{description} must select elements or attributes.");
			}
			if (!decimal.TryParse(node.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
			{
				throw new InvalidDataException($"{description} contains a value that is not a valid decimal.");
			}

			try
			{
				total = checked(total + value);
			}
			catch (OverflowException exception)
			{
				throw new InvalidDataException($"{description} total exceeds the supported decimal range.", exception);
			}
			count++;
		}

		if (count == 0)
		{
			throw new InvalidDataException($"{description} did not match a value.");
		}
		return total;
	}

	private static XDocument ParseResponseXml(string xmlResponse)
	{
		xmlResponse = xmlResponse.TrimStart('\uFEFF').Trim();
		while (xmlResponse.StartsWith('"') || xmlResponse.StartsWith("<?xml version=\\", StringComparison.Ordinal))
		{
			var json = xmlResponse.StartsWith('"') ? xmlResponse : $"\"{xmlResponse}\"";
			xmlResponse = JsonSerializer.Deserialize<string>(json)!;
		}

		using var reader = XmlReader.Create(new StringReader(xmlResponse), new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Prohibit,
			XmlResolver = null
		});
		return XDocument.Load(reader);
	}
}
