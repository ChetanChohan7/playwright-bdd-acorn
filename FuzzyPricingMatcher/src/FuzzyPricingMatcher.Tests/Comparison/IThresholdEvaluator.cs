namespace FuzzyPricingMatcher.Tests.Comparison;

public interface IThresholdEvaluator
{
    ThresholdResult Evaluate(decimal apiValue, decimal baselineValue, decimal minimumThreshold, decimal maximumThreshold);
}