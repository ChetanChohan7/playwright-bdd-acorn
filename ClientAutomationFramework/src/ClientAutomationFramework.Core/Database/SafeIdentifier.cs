using System.Text.RegularExpressions;

namespace ClientAutomationFramework.Core.Database;

/// Table names come from configuration and are interpolated into SQL text (identifiers can't be
/// sent as query parameters), so every one is checked against this pattern first.
internal static partial class SafeIdentifier
{
    public static string Validate(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !Pattern().IsMatch(identifier))
            throw new InvalidOperationException($"'{identifier}' is not a safe SQL table identifier.");
        return identifier;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$")]
    private static partial Regex Pattern();
}
