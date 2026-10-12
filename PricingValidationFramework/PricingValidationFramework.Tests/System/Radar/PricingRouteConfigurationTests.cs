using PricingValidationFramework.Core.Configuration;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class PricingRouteConfigurationTests
{
    private string directory = string.Empty;

    private static string FixtureMapping =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "Radar", "Fixtures", "mappings.json");

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"route-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [Test]
    public void Routing_only_load_should_match_the_routes_of_the_full_load()
    {
        var routes = PricingRouteConfiguration.LoadRoutes(FixtureMapping);
        var full = PricingProfileConfiguration.Load(FixtureMapping);

        var fromRoutes = routes.CreateRadarRoutes().Single();
        var fromFull = full.CreateRadarRoutes().Single();
        Assert.Multiple(() =>
        {
            Assert.That(fromRoutes.Key, Is.EqualTo(fromFull.Key));
            Assert.That(fromRoutes.Value.ProductCode, Is.EqualTo(fromFull.Value.ProductCode));
            Assert.That(fromRoutes.Value.SchemeCodes, Is.EqualTo(fromFull.Value.SchemeCodes));
            Assert.That(fromRoutes.Value.EndpointName, Is.EqualTo(fromFull.Value.EndpointName));
            Assert.That(fromRoutes.Value.RouteKey, Is.EqualTo(fromFull.Value.RouteKey));
        });
    }

    [TestCase("HOME", "ABC", "Route001")]
    [TestCase(" HOME ", " POL ", "Route001")]
    public void FindRoute_should_match_product_and_scheme_exactly_after_trimming(string product, string scheme, string expectedProfile)
    {
        var route = PricingRouteConfiguration.LoadRoutes(FixtureMapping).FindRoute(product, scheme);

        Assert.Multiple(() =>
        {
            Assert.That(route?.ProfileId, Is.EqualTo(expectedProfile));
            Assert.That(route?.EndpointName, Is.EqualTo("Endpoint1"));
            Assert.That(route?.RouteKey, Is.EqualTo("shared-home"));
        });
    }

    [TestCase("home", "ABC")]
    [TestCase("HOME", "abc")]
    [TestCase("MOTOR", "ABC")]
    public void FindRoute_should_return_null_when_nothing_matches_exactly(string product, string scheme)
    {
        Assert.That(PricingRouteConfiguration.LoadRoutes(FixtureMapping).FindRoute(product, scheme), Is.Null);
    }

    [TestCase("missingPathsFile")]
    [TestCase("invalidXPath")]
    [TestCase("missingIceField")]
    [TestCase("noPathsOrIceField")]
    public void Routing_only_load_should_ignore_problems_in_the_extraction_files_that_the_full_load_rejects(string problem)
    {
        var mapping = WriteMapping(problem switch
        {
            "missingPathsFile" => Profile(pathsFile: "does-not-exist.json"),
            "missingIceField" => Profile(iceField: "NotInPaths"),
            "noPathsOrIceField" => Profile(pathsFile: "", iceField: ""),
            _ => Profile()
        });
        WritePaths(problem == "invalidXPath" ? "/Response/[" : "/Response/Premium");

        Assert.Multiple(() =>
        {
            Assert.That(PricingRouteConfiguration.LoadRoutes(mapping).Routes, Has.Count.EqualTo(1));
            Assert.That(() => PricingProfileConfiguration.Load(mapping), Throws.InstanceOf<Exception>());
        });
    }

    [TestCase("noEndpoint")]
    [TestCase("noRouteKey")]
    [TestCase("noSchemes")]
    [TestCase("duplicateScheme")]
    public void Routing_only_load_should_still_reject_bad_routing(string problem)
    {
        var mapping = WriteMapping(problem switch
        {
            "noEndpoint" => Profile(endpoint: ""),
            "noRouteKey" => Profile(routeKey: ""),
            "noSchemes" => Profile(schemes: "[]"),
            _ => Profile() + "," + Profile(profileId: "P2")
        });

        Assert.Throws<InvalidDataException>(() => PricingRouteConfiguration.LoadRoutes(mapping));
    }

    [Test]
    public void ResolveMappingPath_should_resolve_relative_paths_against_the_base_folder_and_normalise_separators()
    {
        var resolved = PricingProfileConfiguration.ResolveMappingPath(@" Radar\mappings.json ", directory);

        Assert.That(resolved, Is.EqualTo(Path.Combine(directory, "Radar", "mappings.json")));
    }

    [Test]
    public void ResolveMappingPath_should_keep_an_absolute_path()
    {
        var absolute = Path.Combine(directory, "elsewhere", "mappings.json");

        Assert.That(PricingProfileConfiguration.ResolveMappingPath(absolute, "/some/other/base"), Is.EqualTo(absolute));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void ResolveMappingPath_should_require_a_setting(string? setting)
    {
        Assert.That(() => PricingProfileConfiguration.ResolveMappingPath(setting, directory),
            Throws.InvalidOperationException.With.Message.Contains("PricingProfiles:MappingFile is required"));
    }

    [Test]
    public void The_shared_radar_mapping_should_be_copied_to_the_output_with_all_its_xpath_files()
    {
        var mapping = Path.Combine(TestContext.CurrentContext.TestDirectory, "Radar", "mappings.json");

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(mapping), Is.True, "config/radar/mappings.json is copied to Radar/mappings.json");
            Assert.That(PricingProfileConfiguration.Load(mapping).Profiles, Has.Count.EqualTo(16), "every referenced XPath file is present and valid");
            Assert.That(PricingRouteConfiguration.LoadRoutes(mapping).Routes, Has.Count.EqualTo(16));
        });
    }

    private string WriteMapping(string profiles)
    {
        var path = Path.Combine(directory, "mappings.json");
        File.WriteAllText(path, $$"""{ "profiles": [ {{profiles}} ] }""");
        return path;
    }

    private void WritePaths(string premiumXPath)
    {
        File.WriteAllText(Path.Combine(directory, "paths.json"), $$"""{ "fields": { "Premium": "{{premiumXPath}}" } }""");
    }

    private static string Profile(
        string profileId = "P1",
        string schemes = "[\"ABC\"]",
        string endpoint = "Endpoint1",
        string routeKey = "home-abc",
        string pathsFile = "paths.json",
        string iceField = "Premium") =>
        $$"""
        { "profileId": "{{profileId}}", "productCode": "HOME", "schemeCodes": {{schemes}},
          "radar": { "endpointName": "{{endpoint}}", "routeKey": "{{routeKey}}" },
          "pathsFile": "{{pathsFile}}", "iceField": "{{iceField}}" }
        """;
}
