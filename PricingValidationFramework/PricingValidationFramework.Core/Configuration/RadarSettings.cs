namespace PricingValidationFramework.Core.Configuration;

public class RadarSettings
{
	public Dictionary<string, RadarEndpointSettings> Endpoints { get; set; } =
		new(StringComparer.Ordinal);

	public Dictionary<string, RadarRouteSettings> Routes { get; set; } =
		new(StringComparer.Ordinal);

	public Dictionary<string, string> ResponseXsdMappings { get; set; } =
		new(StringComparer.Ordinal);
}