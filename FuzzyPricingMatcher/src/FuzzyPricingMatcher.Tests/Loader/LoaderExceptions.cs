namespace FuzzyPricingMatcher.Tests.Loader;

public sealed class LoaderRouteNotFoundException(string message) : InvalidOperationException(message);

public sealed class LoaderRouteDisabledException(string message) : InvalidOperationException(message);

public sealed class LoaderApiException(string message) : InvalidOperationException(message);

public sealed class LoaderResponseValidationException(string message) : InvalidOperationException(message);