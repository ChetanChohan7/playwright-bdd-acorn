namespace PricingValidationFramework.Core.Models.External;

using System.Xml.Serialization;

[XmlRoot("Response")]
public sealed class RadarResponse
{
	// TODO: Add fields only when they are present in the client-approved response schema.
	[XmlElement("TotalAmount")]
	public List<decimal> TotalAmounts { get; set; } = [];

	[XmlIgnore]
	public decimal TotalAmount => TotalAmounts.Count switch
	{
		1 => TotalAmounts[0],
		0 => throw new InvalidDataException("Radar response does not contain a TotalAmount value."),
		_ => throw new InvalidDataException("Radar response contains duplicate TotalAmount values.")
	};
}