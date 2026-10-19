namespace PricingValidationFramework.Core.Configuration;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.XPath;

public sealed class PricingProfileConfiguration
{
	public List<PricingProfile> Profiles { get; set; } = [];

	/// Full load for comparison and ICE: mappings plus every referenced XPath paths file, all checked.
	public static PricingProfileConfiguration Load(string mappingPath)
	{
		var (configuration, fullPath) = ReadMappings(mappingPath, requireExtractionFields: true);
		var files = new Dictionary<string, LoadedPricingPaths>(StringComparer.Ordinal);
		foreach (var profile in configuration.Profiles)
		{
			var pathsFile = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullPath)!, profile.PathsFile
				.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
			if (!files.TryGetValue(pathsFile, out var loadedPaths))
			{
				var paths = JsonSerializer.Deserialize<PricingPaths>(File.ReadAllText(pathsFile), JsonOptions)
					?? throw new InvalidDataException($"Paths file for '{profile.ProfileId}' must contain an object.");
				var fields = paths.Fields;
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
				var sumFields = paths.SumFields ?? [];
				if (sumFields.Any(field => string.IsNullOrWhiteSpace(field) || !fields.ContainsKey(field)) ||
					sumFields.Count != sumFields.Distinct(StringComparer.Ordinal).Count())
				{
					throw new InvalidDataException($"Profile '{profile.ProfileId}' sumFields must contain unique names defined in fields.");
				}
				loadedPaths = new LoadedPricingPaths(fields, new HashSet<string>(sumFields, StringComparer.Ordinal));
				files.Add(pathsFile, loadedPaths);
			}
			if (!loadedPaths.Fields.ContainsKey(profile.IceField))
			{
				throw new InvalidDataException($"Profile '{profile.ProfileId}' Ice field '{profile.IceField}' is not defined in its paths file.");
			}
			profile.Fields = loadedPaths.Fields;
			profile.SumFields = loadedPaths.SumFields;
		}
		return configuration;
	}

	/// Turns the PricingProfiles:MappingFile setting into a full path. A relative value is resolved
	/// against baseDirectory: the test output folder for the tests, the program folder for the loader.
	public static string ResolveMappingPath(string? mappingFile, string baseDirectory)
	{
		if (string.IsNullOrWhiteSpace(mappingFile))
		{
			throw new InvalidOperationException("PricingProfiles:MappingFile is required.");
		}
		ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

		var normalized = mappingFile.Trim().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
		return Path.GetFullPath(Path.Combine(baseDirectory, normalized));
	}

	/// Reads mappings.json and checks what every caller needs: at least one profile, the routing fields,
	/// and unique profile IDs and product/scheme pairs. Paths files are never opened here.
	internal static (PricingProfileConfiguration Configuration, string FullPath) ReadMappings(string mappingPath, bool requireExtractionFields)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mappingPath);
		var fullPath = Path.GetFullPath(mappingPath);
		var configuration = JsonSerializer.Deserialize<PricingProfileConfiguration>(File.ReadAllText(fullPath), JsonOptions)
			?? throw new InvalidDataException("Pricing mappings must contain an object.");
		if (configuration.Profiles is null || configuration.Profiles.Count == 0)
		{
			throw new InvalidDataException("At least one pricing profile must be mapped.");
		}

		var ids = new HashSet<string>(StringComparer.Ordinal);
		var selections = new HashSet<(string Product, string Scheme)>();
		foreach (var profile in configuration.Profiles)
		{
			if (profile is null || string.IsNullOrWhiteSpace(profile.ProfileId) ||
				string.IsNullOrWhiteSpace(profile.ProductCode) || profile.SchemeCodes is null ||
				profile.SchemeCodes.Count == 0 || profile.SchemeCodes.Any(string.IsNullOrWhiteSpace) ||
				profile.Radar is null || string.IsNullOrWhiteSpace(profile.Radar.EndpointName) ||
				string.IsNullOrWhiteSpace(profile.Radar.RouteKey))
			{
				throw new InvalidDataException(requireExtractionFields
					? "Each pricing mapping requires a profile ID, product, schemes, Radar route, paths file and Ice field."
					: "Each pricing mapping requires a profile ID, product, schemes and Radar route.");
			}
			if (requireExtractionFields &&
				(string.IsNullOrWhiteSpace(profile.PathsFile) || string.IsNullOrWhiteSpace(profile.IceField)))
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
		}
		return (configuration, fullPath);
	}

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		AllowDuplicateProperties = false,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
	};

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
		public List<string>? SumFields { get; set; } = [];
	}

	private sealed record LoadedPricingPaths(
		IReadOnlyDictionary<string, string> Fields,
		IReadOnlySet<string> SumFields);
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
	[JsonIgnore]
	public IReadOnlySet<string> SumFields { get; set; } = new HashSet<string>(StringComparer.Ordinal);
}

public sealed class PricingRadarRoute
{
	public string EndpointName { get; set; } = string.Empty;
	public string RouteKey { get; set; } = string.Empty;
}
