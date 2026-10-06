namespace PricingValidationFramework.Core.Models.Reporting;

public sealed record RadarPricingRunResult(
	string RouteId,
	string RadarResponseXml,
	PricingComparisonResult Comparison);