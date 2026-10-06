namespace PricingValidationFramework.Core.Validation;

using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Linq;
using PricingValidationFramework.Core.Models.Reporting;

public class XsdValidator
{
	private readonly ConcurrentDictionary<string, Lazy<XmlSchemaSet>> compiledSchemas = new(StringComparer.OrdinalIgnoreCase);

	public void Preload(string xsdPath)
	{
		GetSchemas(xsdPath);
	}

	private XmlSchemaSet GetSchemas(string xsdPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(xsdPath);
		return compiledSchemas.GetOrAdd(Path.GetFullPath(xsdPath), static path =>
			new Lazy<XmlSchemaSet>(() => CreateSchemaSet(path), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
	}

	public XsdValidationResult Validate(string responseXml, string xsdPath)
	{
		if (string.IsNullOrWhiteSpace(xsdPath))
		{
			throw new ArgumentException("XSD path is required.", nameof(xsdPath));
		}

		var settings = new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Prohibit,
			XmlResolver = null,
			ValidationType = ValidationType.Schema
		};

		settings.Schemas = GetSchemas(xsdPath);

		var errors = new List<string>();
		settings.ValidationEventHandler += (_, args) =>
		{
			errors.Add(args.Message);
		};

		if (string.IsNullOrWhiteSpace(responseXml))
		{
			return new XsdValidationResult(false, new[] { "Response XML is empty." });
		}

		try
		{
			using var reader = XmlReader.Create(new StringReader(responseXml), settings);
			while (reader.Read())
			{
			}
		}
		catch (XmlException ex)
		{
			errors.Add(ex.Message);
		}
		return new XsdValidationResult(errors.Count == 0, errors);
	}

	public XsdDecimalExtractionResult ValidateAndExtractDecimals(string xml, string xsdPath)
	{
		if (string.IsNullOrWhiteSpace(xsdPath))
		{
			throw new ArgumentException("XSD path is required.", nameof(xsdPath));
		}
		if (string.IsNullOrWhiteSpace(xml))
		{
			return new XsdDecimalExtractionResult(false, null, new[] { "XML is empty." });
		}

		var schemas = GetSchemas(xsdPath);
		var errors = new List<string>();
		XDocument? document = null;
		try
		{
			using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Prohibit,
				XmlResolver = null
			});
			document = XDocument.Load(reader);
			document.Validate(schemas, (_, args) => errors.Add(args.Message), addSchemaInfo: true);
		}
		catch (XmlException exception)
		{
			return new XsdDecimalExtractionResult(false, null, new[] { exception.Message });
		}
		catch (XmlSchemaValidationException exception)
		{
			errors.Add(exception.Message);
		}

		if (errors.Count > 0 || document?.Root is null)
		{
			return new XsdDecimalExtractionResult(false, null, errors);
		}

		var values = new List<PricingDecimalValue>();
		ExtractDecimals(document.Root, "/" + document.Root.Name.LocalName, values, errors);
		return errors.Count == 0
			? new XsdDecimalExtractionResult(true, new PricingDocument(values), Array.Empty<string>())
			: new XsdDecimalExtractionResult(false, null, errors);
	}

	private static XmlSchemaSet CreateSchemaSet(string xsdPath)
	{
		var schemaSet = new XmlSchemaSet { XmlResolver = null };
		schemaSet.Add(null, xsdPath);
		schemaSet.Compile();
		return schemaSet;
	}

	private static void AddDecimal(
		string text,
		string path,
		IXmlSchemaInfo? schemaInfo,
		List<PricingDecimalValue> values,
		List<string> errors)
	{
		if (schemaInfo?.SchemaType?.Datatype?.TypeCode != XmlTypeCode.Decimal || schemaInfo.IsNil)
		{
			return;
		}

		if (decimal.TryParse(text, System.Globalization.NumberStyles.Number,
			System.Globalization.CultureInfo.InvariantCulture, out var value))
		{
			values.Add(new PricingDecimalValue(path, value));
		}
		else
		{
			errors.Add($"Decimal field '{path}' cannot be represented as a .NET decimal.");
		}
	}

	private static void ExtractDecimals(
		XElement element,
		string path,
		List<PricingDecimalValue> values,
		List<string> errors)
	{
		var schemaInfo = element.Annotation<IXmlSchemaInfo>();
		if (schemaInfo?.SchemaType?.Datatype?.TypeCode == XmlTypeCode.Decimal && !schemaInfo.IsNil)
		{
			AddDecimal(element.Value, path, schemaInfo, values, errors);
		}
		foreach (var attribute in element.Attributes())
		{
			AddDecimal(attribute.Value, $"{path}/@{attribute.Name.LocalName}", attribute.Annotation<IXmlSchemaInfo>(), values, errors);
		}

		Dictionary<XName, int>? indices = null;
		foreach (var child in element.Elements())
		{
			var schemaElement = child.Annotation<IXmlSchemaInfo>()?.SchemaElement;
			var repeated = schemaElement is not null &&
				(schemaElement.MaxOccursString == "unbounded" || schemaElement.MaxOccurs > 1);
			var childPath = $"{path}/{child.Name.LocalName}";
			if (repeated)
			{
				indices ??= new Dictionary<XName, int>();
				var index = indices.GetValueOrDefault(child.Name);
				indices[child.Name] = index + 1;
				childPath += $"[{index}]";
			}

			ExtractDecimals(child, childPath, values, errors);
		}
	}
}

public sealed class XsdValidationResult
{
	public XsdValidationResult(bool isValid, IEnumerable<string> errors)
	{
		IsValid = isValid;
		Errors = errors.ToArray();
	}

	public bool IsValid { get; }
	public IReadOnlyList<string> Errors { get; }
}
