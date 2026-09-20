using System.Xml.Schema;

namespace FuzzyPricingMatcher.Tests.Validation;

public sealed record SchemaRegistration(string Name, string FilePath, XmlSchemaSet SchemaSet);

public class SchemaRegistryException(string message, Exception? innerException = null) : InvalidOperationException(message, innerException);

public sealed class DuplicateSchemaRegistrationException(string message) : SchemaRegistryException(message);

public sealed class SchemaFileNotFoundException(string message) : SchemaRegistryException(message);

public sealed class SchemaCompilationException(string message, Exception? innerException = null) : SchemaRegistryException(message, innerException);