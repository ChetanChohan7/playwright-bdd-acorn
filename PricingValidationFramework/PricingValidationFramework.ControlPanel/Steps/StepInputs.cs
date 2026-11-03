namespace PricingValidationFramework.ControlPanel.Steps;

using System.Globalization;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Validation;

/// Turns what was typed on the page into checked values, using the same rules as the commands, so a
/// mistake is reported on the page straight away rather than part-way through a run. Blank means "not given".
/// Every method throws StepInputException with a message to show next to the form.
public static class StepInputs
{
    public static string? RequestTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            RequestTimeFormatter.Resolve(value);
            return value.Trim();
        }
        catch (ArgumentException)
        {
            throw new StepInputException("Request time must be 'now' or yyyy-MM-ddZHH:mm:ss, e.g. 2026-10-21Z09:00:00.");
        }
    }

    public static int? WholeNumber(string? value, string label, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= min && number <= max
            ? number
            : throw new StepInputException($"{label} must be a whole number from {min} to {max}.");
    }

    public static decimal? Threshold(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Float, like the Radar tests: a '.' decimal point and no thousands separators, so "0,5" is rejected
        // rather than read as 5.
        return decimal.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new StepInputException($"{label} must be a number with a '.' decimal point, e.g. -0.5 or 2.");
    }

    public static void ThresholdRange(decimal? min, decimal? max)
    {
        if (min > max)
        {
            throw new StepInputException("The minimum threshold must not be more than the maximum.");
        }
    }

    public static ThresholdType? ThresholdTypeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var name = Enum.GetNames<ThresholdType>().FirstOrDefault(name => name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        return name is not null ? Enum.Parse<ThresholdType>(name) : throw new StepInputException("Threshold type must be Pound or Percentage.");
    }

    public static string? ProductCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 100 && !trimmed.Any(char.IsControl)
            ? trimmed
            : throw new StepInputException("Product code must be at most 100 characters on one line.");
    }

    public static string? ScenarioTag(string? value)
    {
        try
        {
            var tag = ScenarioTags.ValidateFilter(value, "Scenario tag");
            return tag.Length == 0 ? null : tag;
        }
        catch (InvalidOperationException exception)
        {
            throw new StepInputException(exception.Message);
        }
    }

    public static string Status(string? value)
    {
        var status = value?.Trim().ToUpperInvariant();
        return status is "PASS" or "FAIL" ? status : throw new StepInputException("Choose PASS or FAIL.");
    }
}

public sealed class StepInputException(string message) : Exception(message);
