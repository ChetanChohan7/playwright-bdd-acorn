namespace PricingValidationFramework.Core.Matching;

public class ThresholdMatcher
{
	public bool IsWithinThreshold(decimal difference, decimal minThreshold, decimal maxThreshold)
	{
		if (minThreshold > maxThreshold)
		{
			throw new ArgumentException("MinThreshold must be less than or equal to MaxThreshold.", nameof(minThreshold));
		}

		return difference >= minThreshold && difference <= maxThreshold;
	}
}
