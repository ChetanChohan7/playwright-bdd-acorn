namespace FuzzyPricingMatcher.Tests.Comparison;

public sealed class ThresholdEvaluator
{
    public ThresholdResult Evaluate(decimal apiValue, decimal baselineValue, decimal minimumThreshold, decimal maximumThreshold)
    {
        if (minimumThreshold > maximumThreshold)
            throw new ArgumentException("Minimum threshold must be less than or equal to maximum threshold.");
        var difference = apiValue - baselineValue;
        return new ThresholdResult(difference >= minimumThreshold && difference <= maximumThreshold, difference);
    }
}