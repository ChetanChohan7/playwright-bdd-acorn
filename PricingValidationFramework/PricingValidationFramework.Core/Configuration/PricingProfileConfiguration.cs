namespace PricingValidationFramework.Core.Configuration;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.XPath;

public sealed class PricingProfileConfiguration
{
	public List<PricingProfile> Profiles { get; set; } = [];

	public static PricingProfileConfiguration Load(string mappingPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mappingPath);
		var fullPath = Path.GetFullPath(mappingPath);
		var options = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			AllowDuplicateProperties = false,
			UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
		};
		var configuration = JsonSerializer.Deserialize<PricingProfileConfiguration>(File.ReadAllText(fullPath), options)
			?? throw new InvalidDataException("Pricing mappings must contain an object.");
		if (configuration.Profiles is null || configuration.Profiles.Count == 0)
		{
			throw new InvalidDataException("At least one pricing profile must be mapped.");
		}

		var ids = new HashSet<string>(StringComparer.Ordinal);
		var selections = new HashSet<(string Product, string Scheme)>();
		var files = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
		foreach (var profile in configuration.Profiles)
		{
			if (profile is null || string.IsNullOrWhiteSpace(profile.ProfileId) ||
				string.IsNullOrWhiteSpace(profile.ProductCode) || profile.SchemeCodes is null ||
				profile.SchemeCodes.Count == 0 || profile.SchemeCodes.Any(string.IsNullOrWhiteSpace) ||
				string.IsNullOrWhiteSpace(profile.PathsFile) || string.IsNullOrWhiteSpace(profile.IceField) ||
				profile.Radar is null || string.IsNullOrWhiteSpace(profile.Radar.EndpointName) ||
				string.IsNullOrWhiteSpace(profile.Radar.RouteKey))
			{
				throw new InvalidDataException("Each pricing mapping requires a profile ID, product, schemes, Radar route, paths file and Ice field.");
			}
			profile.ProfileId = profile.ProfileId.Trim();
			profile.ProductCode = profile.ProductCode.Trim();
			profile.SchemeCodes = profile.SchemeCodes.Select(scheme => scheme.Trim()).ToList();
			if (!ids.Add(profile.ProfileId) ||
				profile.SchemeCodes.Any(scheme => !selections.Add((profile.ProductCode, scheme))))
			{
				throw new InvalidDataException($"Duplicate profile ID or product/scheme mapping for '{profile.ProfileId}'.");
			}

			var pathsFile = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullPath)!, profile.PathsFile
				.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
			if (!files.TryGetValue(pathsFile, out var fields))
			{
				var paths = JsonSerializer.Deserialize<PricingPaths>(File.ReadAllText(pathsFile), options)
					?? throw new InvalidDataException($"Paths file for '{profile.ProfileId}' must contain an object.");
				fields = paths.Fields;
				if (fields is null || fields.Count == 0 || fields.Any(field =>
					string.IsNullOrWhiteSpace(field.Key) || string.IsNullOrWhiteSpace(field.Value)))
				{
					throw new InvalidDataException($"Profile '{profile.ProfileId}' requires named XPath fields.");
				}
				foreach (var field in fields)
				{
					try
					{
						XPathExpression.Compile(field.Value);
					}
					catch (XPathException exception)
					{
						throw new InvalidDataException($"Profile '{profile.ProfileId}', field '{field.Key}' has an invalid XPath '{field.Value}'.", exception);
					}
				}
				files.Add(pathsFile, fields);
			}
			if (!fields.ContainsKey(profile.IceField))
			{
				throw new InvalidDataException($"Profile '{profile.ProfileId}' Ice field '{profile.IceField}' is not defined in its paths file.");
			}
			profile.Fields = fields;
		}
		return configuration;
	}

	public Dictionary<string, RadarRouteSettings> CreateRadarRoutes()
	{
		return Profiles.ToDictionary(profile => profile.ProfileId, profile => new RadarRouteSettings
		{
			ProductCode = profile.ProductCode,
			SchemeCodes = profile.SchemeCodes.ToList(),
			EndpointName = profile.Radar.EndpointName,
			RouteKey = profile.Radar.RouteKey
		}, StringComparer.Ordinal);
	}

	private sealed class PricingPaths
	{
		public Dictionary<string, string> Fields { get; set; } = new(StringComparer.Ordinal);
	}
}

public sealed class PricingProfile
{
	public string ProfileId { get; set; } = string.Empty;
	public string ProductCode { get; set; } = string.Empty;
	public List<string> SchemeCodes { get; set; } = [];
	public PricingRadarRoute Radar { get; set; } = new();
	public string PathsFile { get; set; } = string.Empty;
	public string IceField { get; set; } = string.Empty;
	[JsonIgnore]
	public IReadOnlyDictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
}

public sealed class PricingRadarRoute
{
	public string EndpointName { get; set; } = string.Empty;
	public string RouteKey { get; set; } = string.Empty;
}
