namespace PricingValidationFramework.Core.Validation;

/// The one rule for scenario tags, shared by the loader (writing Test_tags) and the Radar/ICE
/// SCENARIO_TAG filter (reading them): letters, digits, '_', '-' and '.', at most 64 characters.
/// Test_tags stores them comma-separated, e.g. "smoke,regression".
public static class ScenarioTags
{
	public const int MaxTagLength = 64;

	public static bool IsValid(string? tag) =>
		!string.IsNullOrWhiteSpace(tag) &&
		tag.Length <= MaxTagLength &&
		tag.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');

	/// Splits on ',' or ';', trims, removes duplicates (ignoring case) and joins with ','. Empty input
	/// gives "". Throws InvalidDataException naming the first invalid tag.
	public static string Normalize(string? tags)
	{
		var parts = (tags ?? string.Empty)
			.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		var invalid = parts.FirstOrDefault(tag => !IsValid(tag));
		if (invalid is not null)
		{
			throw new InvalidDataException(
				$"Test tag '{invalid}' is invalid: use letters, digits, '_', '-' or '.' (at most {MaxTagLength} characters), separated by commas.");
		}

		return string.Join(',', parts);
	}

	/// For a filter value: trimmed, or empty when not set. Throws when it isn't a single valid tag.
	public static string ValidateFilter(string? tag, string settingName)
	{
		if (string.IsNullOrWhiteSpace(tag))
		{
			return string.Empty;
		}

		var trimmed = tag.Trim();
		return IsValid(trimmed)
			? trimmed
			: throw new InvalidOperationException(
				$"{settingName} must be one tag of letters, digits, '_', '-' or '.' (at most {MaxTagLength} characters).");
	}
}
