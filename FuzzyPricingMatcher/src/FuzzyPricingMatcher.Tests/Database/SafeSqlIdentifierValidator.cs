using System.Text.RegularExpressions;

namespace FuzzyPricingMatcher.Tests.Database;

public static partial class SafeSqlIdentifierValidator
{
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    public static string Validate(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !IdentifierPattern().IsMatch(identifier))
            throw new DatabaseOperationException($"Unsafe SQL identifier '{identifier}'. Expected a schema-qualified identifier such as dbo.tb_response.");
        return identifier;
    }
}