using FuzzyPricingMatcher.Tests.Validation;

namespace FuzzyPricingMatcher.Tests.Processing;

public sealed record ComparisonAmountReaderRegistration(string Name, IResponseAmountReader Reader);

public sealed class ComparisonAmountReaderRegistry
{
    private readonly IReadOnlyDictionary<string, IResponseAmountReader> readers;

    public ComparisonAmountReaderRegistry(IEnumerable<ComparisonAmountReaderRegistration> registrations)
    {
        var readerRegistrations = registrations.ToArray();
        if (readerRegistrations.GroupBy(registration => registration.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Duplicate response amount reader registrations are not allowed.");
        readers = readerRegistrations.ToDictionary(registration => registration.Name, registration => registration.Reader, StringComparer.OrdinalIgnoreCase);
    }

    public IResponseAmountReader Resolve(string name) => readers.TryGetValue(name, out var reader)
        ? reader
        : throw new InvalidOperationException($"Response amount reader '{name}' is not registered.");
}

public sealed record ValidatedResponseAmount(decimal Amount, XmlValidationResult Validation);

public sealed class ExternalResponseValidationService
{
    private readonly XmlSchemaValidator validator;
    private readonly ComparisonAmountReaderRegistry amountReaders;

    public ExternalResponseValidationService(XmlSchemaValidator validator, ComparisonAmountReaderRegistry amountReaders)
    {
        this.validator = validator;
        this.amountReaders = amountReaders;
    }

    public ValidatedResponseAmount ValidateAndReadAmount(XmlResponseValidationRequest request, string amountReaderName)
    {
        var validationResult = validator.Validate(request);
        var amountReader = amountReaders.Resolve(amountReaderName);
        return new ValidatedResponseAmount(amountReader.ReadAmount(validationResult.Document), validationResult);
    }
}
