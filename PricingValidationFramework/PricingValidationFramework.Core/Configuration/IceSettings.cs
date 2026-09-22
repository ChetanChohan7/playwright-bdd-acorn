namespace PricingValidationFramework.Core.Configuration;

public class IceSettings
{
	public string IceEndpoint { get; set; } = string.Empty;
	public string ApiKeyHeaderName { get; set; } = string.Empty;
	public string ApiKeyHeaderValue { get; set; } = string.Empty;
	public string CertificateHost { get; set; } = string.Empty;
	public string PfxCertificateFile { get; set; } = string.Empty;
	public string CertificatePassword { get; set; } = string.Empty;
	public string PfxCertificateBase64 { get; set; } = string.Empty;
}
