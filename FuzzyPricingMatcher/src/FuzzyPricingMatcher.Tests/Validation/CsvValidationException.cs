namespace FuzzyPricingMatcher.Tests.Validation;

public sealed class CsvValidationException(string message) : InvalidOperationException(message);