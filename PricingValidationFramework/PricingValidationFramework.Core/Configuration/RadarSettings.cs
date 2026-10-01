namespace PricingValidationFramework.Core.Configuration;

public class RadarSettings
{
	public Dictionary<string, RadarEndpointSettings> Endpoints { get; set; } =
		new(StringComparer.Ordinal);

	public Dictionary<string, RadarRouteSettings> Routes { get; set; } =
		new(StringComparer.Ordinal);

	// Feature flag for Radar response XSD validation, off by default until the approved schemas are received.
	// When false, responses are not validated against ResponseXsdMappings (the mappings are kept);
	// set RadarSettings__ValidateResponseXsd=true to turn it on.
	public bool ValidateResponseXsd { get; set; }

	// TODO: Add approved response schema files here, keyed by the existing route identifier.
	public Dictionary<string, string> ResponseXsdMappings { get; set; } =
		new(StringComparer.Ordinal);
}