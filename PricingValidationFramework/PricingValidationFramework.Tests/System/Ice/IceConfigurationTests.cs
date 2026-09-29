using Microsoft.Extensions.Configuration;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.System.Ice;

[TestFixture]
[NonParallelizable]
public class IceConfigurationTests
{
	[Test]
	public void Ice_configuration_should_bind_hierarchical_environment_overrides()
	{
		const string endpointKey = "IceSettings__IceEndpoint";
		const string retryKey = "RetrySettings__ApiRetryCount";
		const string colonKey = "IceSettings:ApiKeyHeaderName";
		const string secretKey = "IceSettings__ApiKeyHeaderValue";
		const string unrelatedKey = "PVF_REVIEW_UNRELATED_SETTING";
		var originalValues = new Dictionary<string, string?>
		{
			[endpointKey] = Environment.GetEnvironmentVariable(endpointKey),
			[retryKey] = Environment.GetEnvironmentVariable(retryKey),
			[colonKey] = Environment.GetEnvironmentVariable(colonKey),
			[secretKey] = Environment.GetEnvironmentVariable(secretKey),
			[unrelatedKey] = Environment.GetEnvironmentVariable(unrelatedKey)
		};

		try
		{
			Environment.SetEnvironmentVariable(endpointKey, "https://override.example.test/ice");
			Environment.SetEnvironmentVariable(retryKey, "7");
			Environment.SetEnvironmentVariable(colonKey, "X-ICE-COLON-KEY");
			Environment.SetEnvironmentVariable(secretKey, "test-secret-value");
			Environment.SetEnvironmentVariable(unrelatedKey, "unrelated");

			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["IceSettings:IceEndpoint"] = "https://json.example.test/ice",
					["IceSettings:ApiKeyHeaderName"] = "X-JSON-KEY",
					["IceSettings:ApiKeyHeaderValue"] = "json-secret-value",
					["RetrySettings:ApiRetryCount"] = "3"
				})
				.AddInMemoryCollection(IceTestSetup.GetEnvironmentVariables())
				.Build();
			var iceSettings = configuration.GetSection("IceSettings").Get<IceSettings>()!;
			var retrySettings = configuration.GetSection("RetrySettings").Get<RetrySettings>()!;

			Assert.Multiple(() =>
			{
				Assert.That(string.Equals(iceSettings.IceEndpoint, "https://override.example.test/ice", StringComparison.Ordinal), Is.True);
				Assert.That(string.Equals(iceSettings.ApiKeyHeaderName, "X-ICE-COLON-KEY", StringComparison.Ordinal), Is.True);
				Assert.That(string.Equals(iceSettings.ApiKeyHeaderValue, "test-secret-value", StringComparison.Ordinal), Is.True);
				Assert.That(retrySettings.ApiRetryCount, Is.EqualTo(7));
				Assert.That(configuration[unrelatedKey], Is.EqualTo("unrelated"));
			});
		}
		finally
		{
			foreach (var originalValue in originalValues)
			{
				Environment.SetEnvironmentVariable(originalValue.Key, originalValue.Value);
			}
		}
	}

	[Test]
	public void Ice_settings_should_reject_missing_endpoint_header_and_credential_without_exposing_secrets()
	{
		const string secret = "do-not-include-this-secret";
		var settings = ValidIceSettings();
		settings.IceEndpoint = string.Empty;
		settings.ApiKeyHeaderValue = secret;

		var exception = Assert.Throws<ArgumentException>(settings.Validate);

		Assert.Multiple(() =>
		{
			Assert.That(exception!.Message, Does.Contain("IceSettings:IceEndpoint"));
			Assert.That(exception.Message, Does.Not.Contain(secret));
		});

		settings = ValidIceSettings();
		settings.ApiKeyHeaderName = string.Empty;
		Assert.Throws<ArgumentException>(settings.Validate);

		settings = ValidIceSettings();
		settings.ApiKeyHeaderValue = string.Empty;
		Assert.Throws<ArgumentException>(settings.Validate);

		settings = ValidIceSettings();
		settings.PfxCertificateBase64 = string.Empty;
		Assert.Throws<ArgumentException>(settings.Validate);
	}

	[Test]
	public void Ice_settings_should_reject_malformed_or_unavailable_certificate_sources()
	{
		var settings = ValidIceSettings();
		settings.PfxCertificateBase64 = "not-base64";
		Assert.Throws<ArgumentException>(settings.Validate);

		settings = ValidIceSettings();
		settings.PfxCertificateBase64 = string.Empty;
		settings.PfxCertificateFile = "missing-certificate.pfx";
		Assert.Throws<ArgumentException>(settings.Validate);
	}

	private static IceSettings ValidIceSettings()
	{
		return new IceSettings
		{
			IceEndpoint = "https://ice.example.test/quote",
			ApiKeyHeaderName = "X-ICE-API-KEY",
			ApiKeyHeaderValue = "test-secret-value",
			PfxCertificateBase64 = "AQ=="
		};
	}
}