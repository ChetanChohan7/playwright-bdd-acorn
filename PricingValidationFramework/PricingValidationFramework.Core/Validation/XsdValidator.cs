namespace PricingValidationFramework.Core.Validation;

using System.Xml;
using System.Xml.Schema;

public class XsdValidator
{
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

		var schemaSet = new XmlSchemaSet();
		schemaSet.Add(null, xsdPath);
		schemaSet.Compile();
		settings.Schemas = schemaSet;

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
