using System.Text.RegularExpressions;

namespace FuzzyPricingMatcher.Tests.Infrastructure;

public static partial class SafeLogValue
{
    public static string Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var redacted = SensitiveAssignmentRegex().Replace(value, "$1[REDACTED]");
        return redacted.Contains('<') && redacted.Contains('>') ? "[REDACTED_XML]" : redacted;
    }

    [GeneratedRegex("(?i)(password|pwd|authorization|basic|connectionstring|user id|username)\\s*[:=]\\s*[^;,& ]+", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignmentRegex();
}