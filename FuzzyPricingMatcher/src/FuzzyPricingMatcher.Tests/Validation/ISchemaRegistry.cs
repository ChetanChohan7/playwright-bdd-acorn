namespace FuzzyPricingMatcher.Tests.Validation;

public interface ISchemaRegistry
{
    SchemaRegistration Resolve(string schemaFileName);
}