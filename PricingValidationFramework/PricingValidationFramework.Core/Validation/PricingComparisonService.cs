namespace PricingValidationFramework.Core.Validation;

using System.Collections.Frozen;
using PricingValidationFramework.Core.Matching;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;

public interface IPricingSchemaProfile
{
	string ProfileId { get; }
	string ProductCode { get; }
	string SchemeCode { get; }
	XsdDecimalExtractionResult ExtractBaseline(string baselineXml);
	PricingComparisonResult CompareExtractedBaseline(PricingDocument baseline, string responseXml, decimal minDelta, decimal maxDelta);
}

public sealed class PricingSchemaProfile : IPricingSchemaProfile
{
	private readonly string xsdPath;
	private readonly XsdValidator xsdValidator;
	private readonly FuzzyPricingMatcher matcher;

	public PricingSchemaProfile(
		string profileId,
		string productCode,
		string schemeCode,
		string xsdPath,
		XsdValidator xsdValidator,
		FuzzyPricingMatcher matcher)
	{
		ProfileId = RequireValue(profileId, nameof(profileId));
		ProductCode = RequireValue(productCode, nameof(productCode));
		SchemeCode = RequireValue(schemeCode, nameof(schemeCode));
		this.xsdPath = RequireValue(xsdPath, nameof(xsdPath));
		this.xsdValidator = xsdValidator ?? throw new ArgumentNullException(nameof(xsdValidator));
		this.matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
	}

	public string ProfileId { get; }
	public string ProductCode { get; }
	public string SchemeCode { get; }

	public XsdDecimalExtractionResult ExtractBaseline(string baselineXml)
	{
		return xsdValidator.ValidateAndExtractDecimals(baselineXml, xsdPath);
	}

	public PricingComparisonResult CompareExtractedBaseline(
		PricingDocument baseline,
		string responseXml,
		decimal minDelta,
		decimal maxDelta)
	{
		ArgumentNullException.ThrowIfNull(baseline);
		if (minDelta > maxDelta)
		{
			throw new ArgumentException("Minimum delta must be less than or equal to maximum delta.", nameof(minDelta));
		}

		var response = xsdValidator.ValidateAndExtractDecimals(responseXml, xsdPath);
		if (!response.IsValid)
		{
			return new PricingComparisonResult(
				Array.Empty<DecimalFieldComparison>(),
				string.Join(" | ", response.Errors),
				"ResponseXsdValidation",
				ScenarioResult.Fail,
				ProfileId);
		}

		var comparison = matcher.Compare(baseline, response.Document!, minDelta, maxDelta);
		return new PricingComparisonResult(
			comparison.Fields,
			comparison.Error,
			comparison.FailureStage,
			comparison.Result,
			ProfileId);
	}

	private static string RequireValue(string value, string parameterName)
	{
		return string.IsNullOrWhiteSpace(value)
			? throw new ArgumentException("A value is required.", parameterName)
			: value.Trim();
	}
}

public sealed class PricingComparisonService
{
	private readonly FrozenDictionary<(string ProductCode, string SchemeCode), IPricingSchemaProfile> profiles;

	public PricingComparisonService(IEnumerable<IPricingSchemaProfile> profiles)
	{
		var registeredProfiles = profiles?.ToArray() ?? throw new ArgumentNullException(nameof(profiles));
		var duplicateProfile = registeredProfiles
			.GroupBy(profile => (profile.ProductCode, profile.SchemeCode))
			.FirstOrDefault(group => group.Count() > 1);
		if (duplicateProfile is not null)
		{
			throw new ArgumentException(
				$"More than one pricing schema profile is registered for ProductCode '{duplicateProfile.Key.ProductCode}' and SchemeCode '{duplicateProfile.Key.SchemeCode}'.",
				nameof(profiles));
		}
		this.profiles = registeredProfiles.ToFrozenDictionary(profile => (profile.ProductCode, profile.SchemeCode));
	}

	public PricingComparisonResult Compare(
		string productCode,
		string schemeCode,
		string baselineXml,
		string responseXml,
		decimal minDelta,
		decimal maxDelta)
	{
		var profile = FindProfile(productCode, schemeCode);
		if (profile is null)
		{
			return new PricingComparisonResult(
				Array.Empty<DecimalFieldComparison>(),
				$"No pricing schema profile is configured for ProductCode '{productCode}' and SchemeCode '{schemeCode}'.",
				"SchemaProfileResolution");
		}

		var baseline = profile.ExtractBaseline(baselineXml);
		if (!baseline.IsValid)
		{
			return new PricingComparisonResult(
				Array.Empty<DecimalFieldComparison>(),
				string.Join(" | ", baseline.Errors),
				"BaselineXsdValidation",
				schemaProfile: profile.ProfileId);
		}

		return profile.CompareExtractedBaseline(baseline.Document!, responseXml, minDelta, maxDelta);
	}

	public bool HasProfile(string productCode, string schemeCode)
	{
		return FindProfile(productCode, schemeCode) is not null;
	}

	public PricingBaselineValidationResult ValidateBaseline(string productCode, string schemeCode, string baselineXml)
	{
		var profile = FindProfile(productCode, schemeCode);
		if (profile is null)
		{
			return new PricingBaselineValidationResult(
				false,
				string.Empty,
				FailureStage: "SchemaProfileResolution",
				Error: $"No pricing schema profile is configured for ProductCode '{productCode}' and SchemeCode '{schemeCode}'.");
		}

		var extraction = profile.ExtractBaseline(baselineXml);
		return extraction.IsValid
			? new PricingBaselineValidationResult(true, profile.ProfileId, extraction.Document)
			: new PricingBaselineValidationResult(
				false,
				profile.ProfileId,
				FailureStage: "BaselineXsdValidation",
				Error: string.Join(" | ", extraction.Errors));
	}

	public PricingComparisonResult CompareExtractedBaseline(
		string productCode,
		string schemeCode,
		PricingDocument baseline,
		string responseXml,
		decimal minDelta,
		decimal maxDelta)
	{
		var profile = FindProfile(productCode, schemeCode);
		if (profile is null)
		{
			return new PricingComparisonResult(
				Array.Empty<DecimalFieldComparison>(),
				$"No pricing schema profile is configured for ProductCode '{productCode}' and SchemeCode '{schemeCode}'.",
				"SchemaProfileResolution");
		}

		return profile.CompareExtractedBaseline(baseline, responseXml, minDelta, maxDelta);
	}

	private IPricingSchemaProfile? FindProfile(string productCode, string schemeCode)
	{
		return profiles.GetValueOrDefault((productCode.Trim(), schemeCode.Trim()));
	}
}