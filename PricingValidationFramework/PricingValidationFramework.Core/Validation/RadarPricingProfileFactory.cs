namespace PricingValidationFramework.Core.Validation;

using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Matching;
using PricingValidationFramework.Core.Models.Reporting;

public sealed class RadarPricingProfileFactory
{
	private readonly XsdFileResolver xsdFileResolver;
	private readonly XsdValidator xsdValidator;
	private readonly FuzzyPricingMatcher matcher;

	public RadarPricingProfileFactory(
		XsdFileResolver xsdFileResolver,
		XsdValidator xsdValidator,
		FuzzyPricingMatcher matcher)
	{
		this.xsdFileResolver = xsdFileResolver ?? throw new ArgumentNullException(nameof(xsdFileResolver));
		this.xsdValidator = xsdValidator ?? throw new ArgumentNullException(nameof(xsdValidator));
		this.matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
	}

	public PricingComparisonService Create(RadarSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);
		if (settings.Routes is null || settings.Routes.Count == 0)
		{
			throw new InvalidOperationException("At least one Radar route must be configured.");
		}

		var profiles = new List<IPricingSchemaProfile>();
		foreach (var route in settings.Routes)
		{
			if (settings.ResponseXsdMappings is null ||
				!settings.ResponseXsdMappings.TryGetValue(route.Key, out var xsdFile) ||
				string.IsNullOrWhiteSpace(xsdFile))
			{
				throw new InvalidOperationException($"Radar route '{route.Key}' requires a ResponseXsdMappings entry.");
			}

			var xsdPath = xsdFileResolver.Resolve(xsdFile);
			xsdValidator.Preload(xsdPath);
			foreach (var schemeCode in route.Value.SchemeCodes)
			{
				profiles.Add(new PricingSchemaProfile(
					route.Key, route.Value.ProductCode, schemeCode, xsdPath, xsdValidator, matcher));
			}
		}

		return new PricingComparisonService(profiles);
	}
}