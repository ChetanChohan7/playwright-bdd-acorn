namespace PricingValidationFramework.Core.Validation;

using System.Collections.Frozen;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Extraction;
using PricingValidationFramework.Core.Matching;
using PricingValidationFramework.Core.Models.Reporting;

public sealed class PricingComparisonService
{
	private readonly FrozenDictionary<(string ProductCode, string SchemeCode), PricingProfile> profiles;
	private readonly XmlValueExtractor extractor = new();
	private readonly FuzzyPricingMatcher matcher = new();

	public PricingComparisonService(IEnumerable<PricingProfile> profiles)
	{
		ArgumentNullException.ThrowIfNull(profiles);
		this.profiles = profiles.SelectMany(profile => profile.SchemeCodes.Select(scheme =>
			new KeyValuePair<(string, string), PricingProfile>((profile.ProductCode, scheme), profile)))
			.ToFrozenDictionary();
	}

	public PricingProfile GetProfile(string productCode, string schemeCode)
	{
		return profiles.GetValueOrDefault((productCode.Trim(), schemeCode.Trim()))
			?? throw new InvalidDataException($"No pricing profile is configured for ProductCode '{productCode}' and SchemeCode '{schemeCode}'.");
	}

	public bool HasProfile(string productCode, string schemeCode)
	{
		return profiles.ContainsKey((productCode.Trim(), schemeCode.Trim()));
	}

	public decimal ExtractIceBaseline(string productCode, string schemeCode, string xml)
	{
		var profile = GetProfile(productCode, schemeCode);
		return extractor.ExtractValue(xml, profile.IceField, profile.Fields[profile.IceField]);
	}

	public PricingBaselineValidationResult ValidateBaseline(string productCode, string schemeCode, string baselineXml)
	{
		if (!HasProfile(productCode, schemeCode))
		{
			return new PricingBaselineValidationResult(false, string.Empty,
				FailureStage: "ProfileResolution",
				Error: $"No pricing profile is configured for ProductCode '{productCode}' and SchemeCode '{schemeCode}'.");
		}
		var profile = GetProfile(productCode, schemeCode);
		var extraction = extractor.Extract(baselineXml, profile.Fields);
		return extraction.IsValid
			? new PricingBaselineValidationResult(true, profile.ProfileId, extraction.Document)
			: new PricingBaselineValidationResult(false, profile.ProfileId,
				FailureStage: "BaselineExtraction", Error: string.Join(" | ", extraction.Errors));
	}

	public PricingComparisonResult Compare(string productCode, string schemeCode, string baselineXml,
		string responseXml, decimal minDelta, decimal maxDelta)
	{
		var baseline = ValidateBaseline(productCode, schemeCode, baselineXml);
		return baseline.IsValid
			? CompareExtractedBaseline(productCode, schemeCode, baseline.Document!, responseXml, minDelta, maxDelta)
			: new PricingComparisonResult([], baseline.Error, baseline.FailureStage, profileId: baseline.ProfileId);
	}

	public PricingComparisonResult CompareExtractedBaseline(string productCode, string schemeCode,
		PricingDocument baseline, string responseXml, decimal minDelta, decimal maxDelta)
	{
		if (minDelta > maxDelta)
		{
			throw new ArgumentException("Minimum delta must be less than or equal to maximum delta.", nameof(minDelta));
		}
		var profile = GetProfile(productCode, schemeCode);
		var response = extractor.Extract(responseXml, profile.Fields);
		if (!response.IsValid)
		{
			return new PricingComparisonResult([], string.Join(" | ", response.Errors),
				"ResponseExtraction", profileId: profile.ProfileId);
		}
		var comparison = matcher.Compare(baseline, response.Document!, minDelta, maxDelta);
		return new PricingComparisonResult(comparison.Fields, comparison.Error, comparison.FailureStage,
			comparison.Result, profile.ProfileId);
	}
}
