namespace PricingValidationFramework.Core.Configuration;

/// Routing only: product and scheme to Radar endpoint and route key, read from the same mappings.json
/// as PricingProfileConfiguration but without opening or checking any XPath paths file or Ice field.
/// For callers that call Radar but never compare, such as baseline seeding. Comparison and ICE must
/// use PricingProfileConfiguration.Load, which checks the extraction files as well.
public sealed class PricingRouteConfiguration
{
	private PricingRouteConfiguration(IReadOnlyList<PricingRoute> routes)
	{
		Routes = routes;
	}

	public IReadOnlyList<PricingRoute> Routes { get; }

	public static PricingRouteConfiguration LoadRoutes(string mappingPath)
	{
		var (configuration, _) = PricingProfileConfiguration.ReadMappings(mappingPath, requireExtractionFields: false);
		return new PricingRouteConfiguration(configuration.Profiles
			.Select(profile => new PricingRoute(
				profile.ProfileId,
				profile.ProductCode,
				profile.SchemeCodes.ToArray(),
				profile.Radar.EndpointName.Trim(),
				profile.Radar.RouteKey.Trim()))
			.ToArray());
	}

	/// The route for an exact product and scheme (compared ordinally after trimming, as Radar does), or null.
	public PricingRoute? FindRoute(string productCode, string schemeCode)
	{
		ArgumentNullException.ThrowIfNull(productCode);
		ArgumentNullException.ThrowIfNull(schemeCode);
		var product = productCode.Trim();
		var scheme = schemeCode.Trim();
		return Routes.FirstOrDefault(route =>
			string.Equals(route.ProductCode, product, StringComparison.Ordinal) &&
			route.SchemeCodes.Contains(scheme, StringComparer.Ordinal));
	}

	/// The same route table shape RadarSettings uses, keyed by profile ID.
	public Dictionary<string, RadarRouteSettings> CreateRadarRoutes()
	{
		return Routes.ToDictionary(route => route.ProfileId, route => new RadarRouteSettings
		{
			ProductCode = route.ProductCode,
			SchemeCodes = route.SchemeCodes.ToList(),
			EndpointName = route.EndpointName,
			RouteKey = route.RouteKey
		}, StringComparer.Ordinal);
	}
}

public sealed record PricingRoute(
	string ProfileId,
	string ProductCode,
	IReadOnlyList<string> SchemeCodes,
	string EndpointName,
	string RouteKey);
