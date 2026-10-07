namespace PricingValidationFramework.Core.Models.External;

using System.Text.Json.Serialization;

public sealed class RadarJsonResponse
{
	[JsonPropertyName("response")]
	public string? Response { get; set; }
}