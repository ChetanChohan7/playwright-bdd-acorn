namespace PricingValidationFramework.Core.Models.Enums;

/// How the Radar min/max thresholds are applied to each field (the pipeline's "Threshold type" dropdown).
/// Pound: Radar − baseline, in pounds. Percentage: the % change from the baseline.
public enum ThresholdType
{
    Pound,
    Percentage
}
