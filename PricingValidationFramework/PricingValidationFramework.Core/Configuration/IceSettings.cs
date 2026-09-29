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

	public void Validate()
	{
		if (!Uri.TryCreate(IceEndpoint, UriKind.Absolute, out var endpoint) ||
			(endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
		{
			throw new ArgumentException("IceSettings:IceEndpoint must be an absolute HTTP or HTTPS URL.", nameof(IceEndpoint));
		}

		if (string.IsNullOrWhiteSpace(ApiKeyHeaderName))
		{
			throw new ArgumentException("IceSettings:ApiKeyHeaderName is required.", nameof(ApiKeyHeaderName));
		}

		if (string.IsNullOrWhiteSpace(ApiKeyHeaderValue))
		{
			throw new ArgumentException("IceSettings:ApiKeyHeaderValue is required.", nameof(ApiKeyHeaderValue));
		}

		if (!string.IsNullOrWhiteSpace(PfxCertificateBase64))
		{
			try
			{
				var certificateData = Convert.FromBase64String(PfxCertificateBase64);
				System.Security.Cryptography.CryptographicOperations.ZeroMemory(certificateData);
			}
			catch (FormatException)
			{
				throw new ArgumentException(
					"IceSettings:PfxCertificateBase64 must contain valid Base64 certificate data.",
					nameof(PfxCertificateBase64));
			}

			return;
		}

		if (string.IsNullOrWhiteSpace(PfxCertificateFile))
		{
			throw new ArgumentException("IceSettings:PfxCertificateFile or IceSettings:PfxCertificateBase64 is required.", nameof(PfxCertificateFile));
		}

		if (!File.Exists(PfxCertificateFile))
		{
			throw new ArgumentException("IceSettings:PfxCertificateFile must identify an existing certificate file.", nameof(PfxCertificateFile));
		}
	}
}
