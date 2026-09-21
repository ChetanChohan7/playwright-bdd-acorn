namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public sealed class ExternalAPIConfigurationException(string message) : InvalidOperationException(message);

public sealed class ExternalAPITransportException(string message, Exception? innerException = null) : InvalidOperationException(message, innerException);

public sealed class ExternalAPIResponseException(string message) : InvalidOperationException(message);

public sealed class ExternalAPIRequestException(string message) : InvalidOperationException(message);