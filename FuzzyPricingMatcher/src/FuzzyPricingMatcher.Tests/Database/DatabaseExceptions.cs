namespace FuzzyPricingMatcher.Tests.Database;

public sealed class DatabaseConsistencyException(string message) : InvalidOperationException(message);

public sealed class DatabaseOperationException(string message, Exception? innerException = null) : InvalidOperationException(message, innerException);