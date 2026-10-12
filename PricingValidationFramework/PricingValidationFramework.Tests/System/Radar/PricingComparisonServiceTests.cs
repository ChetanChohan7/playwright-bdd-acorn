using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Validation;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class PricingComparisonServiceTests
{
    private static PricingProfileConfiguration LoadFixture() => PricingProfileConfiguration.Load(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "Radar", "Fixtures", "mappings.json"));

    [Test]
    public void Compare_should_extract_only_configured_fields_and_keep_paths_and_names()
    {
        var result = new PricingComparisonService(LoadFixture().Profiles).Compare("HOME", "A",
            CreateXml("412.30", "49.48", "461.78", "25.00"),
            CreateXml("412.31", "49.48", "461.79", "30.00"), -0.01m, 0.01m);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.Null);
            Assert.That(result.Result, Is.EqualTo(ScenarioResult.Fail));
            Assert.That(result.ProfileId, Is.EqualTo("Route001"));
            Assert.That(result.Fields, Has.Count.EqualTo(4));
            Assert.That(result.PassedFieldCount, Is.EqualTo(3));
            Assert.That(result.FailedFieldCount, Is.EqualTo(1));
            Assert.That(result.Fields.Single(field => field.FieldKey == "AnnualNet").Expected, Is.EqualTo(412.30m));
            Assert.That(result.Fields.Single(field => field.FieldKey == "AdminFee").Delta, Is.EqualTo(5m));
            Assert.That(result.Fields.Single(field => field.FieldKey == "AdminFee").ExpectedPath, Is.EqualTo("/PricingResponse/Fees/AdminFee"));
        });
    }

    [TestCase(true, "BaselineExtraction")]
    [TestCase(false, "ResponseExtraction")]
    public void Extraction_failures_should_be_errors_with_field_and_path(bool invalidBaseline, string stage)
    {
        var valid = CreateXml("1", "2", "3", "4");
        var invalid = CreateXml("invalid", "2", "3", "4");
        var result = new PricingComparisonService(LoadFixture().Profiles).Compare("HOME", "A",
            invalidBaseline ? invalid : valid, invalidBaseline ? valid : invalid, -1m, 1m);
        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(ScenarioResult.Error));
            Assert.That(result.FailureStage, Is.EqualTo(stage));
            Assert.That(result.Error, Does.Contain("AnnualNet").And.Contain("/PricingResponse/Premium/AnnualNet"));
            Assert.That(result.Fields, Is.Empty);
        });
    }

    [Test]
    public void Missing_profile_should_fail_explicitly()
    {
        var result = new PricingComparisonService(LoadFixture().Profiles).Compare("UNKNOWN", "A", "<Response/>", "<Response/>", 0, 0);
        Assert.That(result.FailureStage, Is.EqualTo("ProfileResolution"));
    }

    [TestCase("A")]
    [TestCase("B")]
    public void Schemes_should_share_one_profile_and_route(string scheme)
    {
        var configuration = LoadFixture();
        var service = new PricingComparisonService(configuration.Profiles);
        Assert.Multiple(() =>
        {
            Assert.That(service.GetProfile("HOME", scheme).ProfileId, Is.EqualTo("Route001"));
            Assert.That(configuration.CreateRadarRoutes()["Route001"].RouteKey, Is.EqualTo("shared-home"));
        });
    }

    [Test]
    public void All_16_configured_profiles_should_load_and_resolve_their_fields()
    {
        var configuration = new ConfigurationBuilder().SetBasePath(TestContext.CurrentContext.TestDirectory)
            .AddJsonFile("appsettings.json").Build();
        var mappings = TestConfigurationLoader.LoadProfiles(configuration);
        var service = new PricingComparisonService(mappings.Profiles);
        Assert.That(mappings.Profiles, Has.Count.EqualTo(16));
        Assert.That(mappings.Profiles.Select(profile => profile.PathsFile).Distinct().Count(), Is.EqualTo(16));
        Assert.That(configuration.GetSection("RadarSettings:Routes").Exists(), Is.False);
        foreach (var profile in mappings.Profiles)
        {
            foreach (var scheme in profile.SchemeCodes)
            {
                var selected = service.GetProfile(profile.ProductCode, scheme);
                Assert.That(selected, Is.SameAs(profile));
                Assert.That(selected.Fields, Is.Not.Empty);
                Assert.That(selected.Fields.ContainsKey(selected.IceField), Is.True);
            }
        }
    }

    [Test]
    public void Ice_should_use_its_configured_field_without_extracting_the_other_radar_fields()
    {
        var profile = LoadFixture().Profiles.Single();
        var service = new PricingComparisonService([profile]);
        const string xml = "<PricingResponse><Premium><AnnualGross>123.45</AnnualGross></Premium></PricingResponse>";
        Assert.That(service.ExtractIceBaseline("HOME", "ABC", xml), Is.EqualTo(123.45m));
        Assert.That(service.ValidateBaseline("HOME", "ABC", xml).IsValid, Is.False);
        profile.IceField = "AdminFee";
        Assert.Throws<InvalidDataException>(() => service.ExtractIceBaseline("HOME", "ABC", xml));
    }

    [Test]
    public void Test_appsettings_should_resolve_its_fixture_without_radar_credentials_for_ice()
    {
        var configuration = new ConfigurationBuilder().SetBasePath(TestContext.CurrentContext.TestDirectory)
            .AddJsonFile("appsettings.PricingProfileTest.json").Build();
        var service = new PricingComparisonService(TestConfigurationLoader.LoadProfiles(configuration).Profiles);
        Assert.That(service.ExtractIceBaseline("HOME", "FIXTURE", CreateXml("1", "2", "3", "4")), Is.EqualTo(3m));
    }

    [TestCase("missingFile")]
    [TestCase("duplicateProduct")]
    [TestCase("duplicateId")]
    [TestCase("missingIceField")]
    [TestCase("invalidXPath")]
    [TestCase("emptyFields")]
    [TestCase("invalidJson")]
    [TestCase("duplicateField")]
    public void Loader_should_reject_invalid_mappings(string problem)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"pricing-profiles-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var mappingPath = Path.Combine(directory, "mappings.json");
        var pathsPath = Path.Combine(directory, "paths.json");
        var mapping = LoadFixture();
        var profile = mapping.Profiles.Single();
        profile.PathsFile = problem == "missingFile" ? "missing.json" : "paths.json";
        if (problem == "missingIceField") profile.IceField = "not-a-field";
        if (problem == "duplicateProduct") profile.SchemeCodes.Add("A");
        if (problem == "duplicateId") mapping.Profiles.Add(profile);
        var fields = problem == "emptyFields" ? new Dictionary<string, string>() :
            new Dictionary<string, string> { ["AnnualGross"] = problem == "invalidXPath" ? "[" : "/Response/Amount" };
        try
        {
            File.WriteAllText(mappingPath, problem == "invalidJson" ? "{" : JsonSerializer.Serialize(mapping));
            File.WriteAllText(pathsPath, problem == "duplicateField"
                ? """{"fields":{"AnnualGross":"/Response/First","AnnualGross":"/Response/Second"}}"""
                : JsonSerializer.Serialize(new { fields }));
            Assert.That(() => PricingProfileConfiguration.Load(mappingPath),
                problem == "missingFile" ? Throws.TypeOf<FileNotFoundException>() :
                problem is "invalidJson" or "duplicateField" ? Throws.TypeOf<JsonException>() : Throws.TypeOf<InvalidDataException>());
        }
        finally
        {
            File.Delete(mappingPath);
            File.Delete(pathsPath);
            Directory.Delete(directory);
        }
    }

    [Test]
    public async Task Comparison_should_isolate_values_across_20000_concurrent_scenarios()
    {
        var service = new PricingComparisonService(LoadFixture().Profiles);
        var completed = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, 20000), new ParallelOptions { MaxDegreeOfParallelism = 8 },
            (index, _) =>
            {
                var xml = CreateXml(index.ToString(CultureInfo.InvariantCulture), "2", "3", "4");
                var result = service.Compare("HOME", index % 2 == 0 ? "A" : "B", xml, xml, -0.01m, 0.01m);
                if (result.Result != ScenarioResult.Pass || result.Fields.Count != 4 ||
                    result.Fields.Single(field => field.FieldKey == "AnnualNet").Expected != index)
                    throw new InvalidOperationException($"Scenario {index} produced an incorrect comparison.");
                Interlocked.Increment(ref completed);
                return ValueTask.CompletedTask;
            });
        Assert.That(completed, Is.EqualTo(20000));
    }

    private static string CreateXml(string net, string tax, string gross, string fee) =>
        $"<PricingResponse><Premium><AnnualNet>{net}</AnnualNet><InsuranceTax>{tax}</InsuranceTax><AnnualGross>{gross}</AnnualGross></Premium><Fees><AdminFee>{fee}</AdminFee></Fees><Unmapped>not-a-decimal</Unmapped></PricingResponse>";
}
