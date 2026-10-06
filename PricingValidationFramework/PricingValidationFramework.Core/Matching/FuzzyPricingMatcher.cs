namespace PricingValidationFramework.Core.Matching;

using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;

public sealed class FuzzyPricingMatcher
{
	public PricingComparisonResult Compare(
		PricingDocument expected,
		PricingDocument actual,
		decimal minDelta,
		decimal maxDelta)
	{
		ArgumentNullException.ThrowIfNull(expected);
		ArgumentNullException.ThrowIfNull(actual);
		if (minDelta > maxDelta)
		{
			throw new ArgumentException("Minimum delta must be less than or equal to maximum delta.", nameof(minDelta));
		}

		var comparisons = new List<DecimalFieldComparison>(Math.Max(expected.Values.Count, actual.Values.Count));
		foreach (var field in expected.Values)
		{
			var hasActual = actual.Values.TryGetValue(field.Key, out var actualValue);
			decimal? delta = hasActual ? actualValue - field.Value : null;
			comparisons.Add(new DecimalFieldComparison(
				field.Key,
				field.Key,
				hasActual ? field.Key : string.Empty,
				field.Value,
				hasActual ? actualValue : null,
				delta,
				delta >= minDelta && delta <= maxDelta ? ScenarioResult.Pass : ScenarioResult.Fail));
		}

		foreach (var field in actual.Values)
		{
			if (!expected.Values.ContainsKey(field.Key))
			{
				comparisons.Add(new DecimalFieldComparison(
					field.Key, string.Empty, field.Key, null, field.Value, null, ScenarioResult.Fail));
			}
		}

		return comparisons.Count == 0
			? new PricingComparisonResult(comparisons, "No decimal fields were available to compare.")
			: new PricingComparisonResult(comparisons);
	}
}