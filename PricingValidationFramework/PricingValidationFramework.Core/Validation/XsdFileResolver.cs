namespace PricingValidationFramework.Core.Validation;

public class XsdFileResolver
{
	public string Resolve(string xsdFile)
	{
		if (string.IsNullOrWhiteSpace(xsdFile))
		{
			throw new ArgumentException("XSD file name is required.", nameof(xsdFile));
		}

		var trimmedFile = xsdFile.Trim();
		if (Path.IsPathRooted(trimmedFile) || trimmedFile.Contains("..", StringComparison.Ordinal))
		{
			throw new ArgumentException("XSD file path must resolve within the trusted XSD directory.", nameof(xsdFile));
		}

		var trustedDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestAssets", "Xsd"));
		var candidatePath = Path.GetFullPath(Path.Combine(trustedDirectory, trimmedFile));
		var relativePath = Path.GetRelativePath(trustedDirectory, candidatePath);

		if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
		{
			throw new InvalidOperationException("XSD file path resolves outside the trusted XSD directory.");
		}

		if (!File.Exists(candidatePath))
		{
			throw new FileNotFoundException($"The configured XSD file '{trimmedFile}' was not found in the trusted XSD directory.", candidatePath);
		}

		return candidatePath;
	}
}
