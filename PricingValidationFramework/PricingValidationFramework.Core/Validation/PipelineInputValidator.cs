namespace PricingValidationFramework.Core.Validation;

using System.Globalization;
using PricingValidationFramework.Core.Models.Common;

public class PipelineInputValidator
{
	public void Validate(PipelineSettings settings)
	{
		if (string.IsNullOrWhiteSpace(settings.BuildId))
		{
			throw new ArgumentException("BuildId is required.", nameof(settings));
		}

		if (settings.MinThreshold > settings.MaxThreshold)
		{
			throw new ArgumentException("MinThreshold must be less than or equal to MaxThreshold.", nameof(settings));
		}

		if (!string.IsNullOrWhiteSpace(settings.RequestTime) &&
			!DateTime.TryParseExact(settings.RequestTime, "yyyy-MM-dd'Z'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
		{
			throw new ArgumentException("RequestTime must use yyyy-MM-ddZHH:mm:ss format.", nameof(settings));
		}
	}
}
