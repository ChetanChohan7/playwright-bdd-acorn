namespace PricingValidationFramework.Core.Matching;

using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;

public sealed class FuzzyPricingMatcher
{
	public PricingComparisonResult Compare(
		PricingDocument expected,
		PricingDocument actual,
		decimal minDelta,
		decimal maxDelta,
		ThresholdType thresholdType = ThresholdType.Pound)
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
			decimal? delta = hasActual ? Difference(field.Value, actualValue, thresholdType) : null;
			comparisons.Add(new DecimalFieldComparison(
				field.Key,
				expected.Paths[field.Key],
				hasActual ? actual.Paths[field.Key] : string.Empty,
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
					field.Key, string.Empty, actual.Paths[field.Key], null, field.Value, null, ScenarioResult.Fail));
			}
		}

		return comparisons.Count == 0
			? new PricingComparisonResult(comparisons, "No decimal fields were available to compare.")
			: new PricingComparisonResult(comparisons);
	}

	/// Pound: Radar − baseline. Percentage: (Radar − baseline) ÷ |baseline| × 100, so a negative value
	/// always means Radar is lower, even for a negative baseline such as a discount. A % change from a
	/// zero baseline can't be calculated: 0 → 0 is no change (0), and 0 → anything else has no
	/// difference (null), which never falls within the thresholds, so the field fails.
	internal static decimal? Difference(decimal baseline, decimal actual, ThresholdType thresholdType)
	{
		if (thresholdType == ThresholdType.Pound)
		{
			return actual - baseline;
		}

		if (baseline == 0)
		{
			return actual == 0 ? 0m : null;
		}

		return Math.Round((actual - baseline) / Math.Abs(baseline) * 100m, 4);
	}
}