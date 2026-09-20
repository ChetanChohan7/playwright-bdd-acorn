namespace FuzzyPricingMatcher.Tests.Api;

public sealed class ApiConfigurationException(string message) : InvalidOperationException(message);

public sealed class ApiTransportException(string message, Exception? innerException = null) : InvalidOperationException(message, innerException);

public sealed class ApiResponseException(string message) : InvalidOperationException(message);

public sealed class ApiRequestValidationException(string message) : InvalidOperationException(message);