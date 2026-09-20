namespace FuzzyPricingMatcher.Tests.Validation;

public class RequestXmlValidationException(string message, Exception? innerException = null) : InvalidOperationException(message, innerException);

public sealed class MissingSchemeCodeException(string message) : RequestXmlValidationException(message);

public sealed class MissingPolicyReferenceException(string message) : RequestXmlValidationException(message);

public sealed class DuplicateXmlMetadataElementException(string message) : RequestXmlValidationException(message);