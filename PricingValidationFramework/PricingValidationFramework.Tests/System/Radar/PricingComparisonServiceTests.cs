using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Matching;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Validation;
using Microsoft.Extensions.Configuration;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class PricingComparisonServiceTests
{
    private readonly string xsdPath = new XsdFileResolver().Resolve("PricingComparisonFixture.xsd");

    [Test]
    public void Compare_should_extract_all_decimal_fields_from_the_same_xsd_contract()
    {
        var result = CreateService().Compare(
            "HOME",
            "A",
            CreateXml("412.30", "49.48", "461.78", "25.00", "baseline"),
            CreateXml("412.31", "49.48", "461.79", "30.00", "response"),
            -0.01m,
            0.01m);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.Null, result.Error);
            Assert.That(result.Result, Is.EqualTo(ScenarioResult.Fail));
            Assert.That(result.SchemaProfile, Is.EqualTo("HOME-A"));
            Assert.That(result.Fields, Has.Count.EqualTo(4));
            Assert.That(result.PassedFieldCount, Is.EqualTo(3));
            Assert.That(result.FailedFieldCount, Is.EqualTo(1));
            Assert.That(result.Fields.Single(field => field.FieldKey == "/PricingResponse/Premium/AnnualNet").Expected, Is.EqualTo(412.30m));
            Assert.That(result.Fields.Single(field => field.FieldKey == "/PricingResponse/Fees/AdminFee").Delta, Is.EqualTo(5m));
            Assert.That(result.Fields.All(field => field.FieldKey != "/PricingResponse/Label"), Is.True);
        });
    }

    [Test]
    public void Compare_should_fail_when_response_does_not_match_its_xsd()
    {
        var result = CreateService().Compare(
            "HOME", "A",
            CreateXml("412.30", "49.48", "461.78", "25.00", "baseline"),
            CreateXml("412.30", "not-decimal", "461.78", "25.00", "response"),
            -0.01m, 0.01m);

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(ScenarioResult.Fail));
            Assert.That(result.FailureStage, Is.EqualTo("ResponseXsdValidation"));
            Assert.That(result.Fields, Is.Empty);
        });
    }

    [Test]
    public void Compare_should_error_when_baseline_does_not_match_its_xsd()
    {
        var result = CreateService().Compare(
            "HOME", "A",
            CreateXml("not-decimal", "49.48", "461.78", "25.00", "baseline"),
            CreateXml("412.30", "49.48", "461.78", "25.00", "response"),
            -0.01m, 0.01m);

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(ScenarioResult.Error));
            Assert.That(result.FailureStage, Is.EqualTo("BaselineXsdValidation"));
            Assert.That(result.Fields, Is.Empty);
        });
    }

    [Test]
    public void Compare_should_error_when_no_profile_matches_product_and_scheme()
    {
        var result = CreateService().Compare(
            "MOTOR", "A",
            CreateXml("1", "2", "3", "4", "baseline"),
            CreateXml("1", "2", "3", "4", "response"),
            -0.01m, 0.01m);

        Assert.That(result.FailureStage, Is.EqualTo("SchemaProfileResolution"));
    }

    [Test]
    [TestCase("A")]
    [TestCase("B")]
    public void Profile_factory_should_use_the_route_xsd_without_field_mappings(string schemeCode)
    {
        var settings = CreateRouteSettings();
        var service = new RadarPricingProfileFactory(
            new XsdFileResolver(),
            new XsdValidator(),
            new FuzzyPricingMatcher()).Create(settings);

        var result = service.Compare(
            "HOME", schemeCode,
            CreateXml("10", "20", "30", "4", "baseline"),
            CreateXml("10.01", "20", "30", "4", "response"),
            -0.01m, 0.01m);

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(ScenarioResult.Pass));
            Assert.That(result.SchemaProfile, Is.EqualTo("Route001"));
            Assert.That(result.Fields, Has.Count.EqualTo(4));
            Assert.That(result.Fields.Single(field => field.FieldKey.EndsWith("/AnnualNet", StringComparison.Ordinal)).ExpectedPath, Does.EndWith("AnnualNet"));
        });
    }

    [Test]
    public void Profile_factory_should_reject_a_route_without_an_xsd_mapping()
    {
        var settings = CreateRouteSettings();
        settings.ResponseXsdMappings.Clear();
        var factory = new RadarPricingProfileFactory(new XsdFileResolver(), new XsdValidator(), new FuzzyPricingMatcher());

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Create(settings));

        Assert.That(exception!.Message, Does.Contain("Route001"));
    }

    [Test]
    public async Task Route_comparison_should_isolate_decimal_values_across_20000_concurrent_scenarios()
    {
        var service = new RadarPricingProfileFactory(
            new XsdFileResolver(), new XsdValidator(), new FuzzyPricingMatcher()).Create(CreateRouteSettings());
        var completed = 0;
        var elapsed = global::System.Diagnostics.Stopwatch.StartNew();

        await Parallel.ForEachAsync(Enumerable.Range(0, 20000), new ParallelOptions { MaxDegreeOfParallelism = 8 },
            (scenarioIndex, _) =>
            {
                var amount = scenarioIndex.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
                var baseline = CreateXml(amount, "2", "3", "4", "baseline")
                    .Replace("<Fees><AdminFee>4</AdminFee></Fees>",
                        "<Fees><AdminFee>4</AdminFee><Surcharge>0.5</Surcharge><Surcharge>0.75</Surcharge></Fees>", StringComparison.Ordinal)
                    .Replace("<PricingResponse xmlns=", "<PricingResponse indexAdjustment=\"1.25\" xmlns=", StringComparison.Ordinal);
                var response = baseline.Replace("<Surcharge>0.75</Surcharge>", "<Surcharge>0.76</Surcharge>", StringComparison.Ordinal);
                var result = service.Compare("HOME", scenarioIndex % 2 == 0 ? "A" : "B", baseline, response, -0.01m, 0.01m);

                if (result.Result != ScenarioResult.Pass || result.Fields.Count != 7 ||
                    result.Fields.Single(field => field.FieldKey.EndsWith("/AnnualNet", StringComparison.Ordinal)).Expected != scenarioIndex ||
                    result.Fields.Single(field => field.FieldKey.EndsWith("/Surcharge[1]", StringComparison.Ordinal)).Delta != 0.01m)
                {
                    throw new InvalidOperationException($"Scenario {scenarioIndex} produced an incorrect comparison: {result.Error}");
                }
                Interlocked.Increment(ref completed);
                return ValueTask.CompletedTask;
            });

        Assert.That(completed, Is.EqualTo(20000));
        TestContext.Out.WriteLine($"Compared {completed} scenarios with seven decimal fields in {elapsed.Elapsed.TotalSeconds:F2} seconds (XML validation and comparison only).");
    }

    [Test]
    public void Profile_factory_should_reject_a_missing_xsd_file()
    {
        var settings = CreateRouteSettings();
        settings.ResponseXsdMappings["Route001"] = "missing-schema.xsd";
        var factory = new RadarPricingProfileFactory(new XsdFileResolver(), new XsdValidator(), new FuzzyPricingMatcher());

        Assert.Throws<FileNotFoundException>(() => factory.Create(settings));
    }

    [Test]
    public void Pricing_profile_test_appsettings_should_bind_and_run_the_fixture_profile()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(TestContext.CurrentContext.TestDirectory)
            .AddJsonFile("appsettings.PricingProfileTest.json", optional: false)
            .Build();
        var radarSettings = configuration.GetSection("RadarSettings").Get<RadarSettings>()!;
        var pipelineSettings = configuration.GetSection("PipelineSettings").Get<global::PricingValidationFramework.Core.Models.Common.PipelineSettings>()!;
        var service = new RadarPricingProfileFactory(
            new XsdFileResolver(),
            new XsdValidator(),
            new FuzzyPricingMatcher()).Create(radarSettings);

        var result = service.Compare(
            "HOME", "FIXTURE",
            CreateXml("412.30", "49.48", "461.78", "25.00", "baseline"),
            CreateXml("412.31", "49.48", "461.79", "25.00", "response"),
            pipelineSettings.MinThreshold,
            pipelineSettings.MaxThreshold);

        Assert.Multiple(() =>
        {
            Assert.That(radarSettings.ResponseXsdMappings, Has.Count.EqualTo(1));
            Assert.That(result.Result, Is.EqualTo(ScenarioResult.Pass));
            Assert.That(result.Fields, Has.Count.EqualTo(4));
        });
    }

    private PricingComparisonService CreateService()
    {
        return new PricingComparisonService(
        [
            new PricingSchemaProfile(
                "HOME-A",
                "HOME",
                "A",
                xsdPath,
                new XsdValidator(),
                new FuzzyPricingMatcher())
        ]);
    }

    private static RadarSettings CreateRouteSettings()
    {
        return new RadarSettings
        {
            Routes = new Dictionary<string, RadarRouteSettings>
            {
                ["Route001"] = new() { ProductCode = "HOME", SchemeCodes = ["A", "B"] }
            },
            ResponseXsdMappings = new Dictionary<string, string>
            {
                ["Route001"] = "PricingComparisonFixture.xsd"
            }
        };
    }

    private static string CreateXml(string annualNet, string insuranceTax, string annualGross, string adminFee, string label)
    {
        return $"""
            <PricingResponse xmlns="urn:pricing-comparison-fixture">
              <Premium>
                <AnnualNet>{annualNet}</AnnualNet>
                <InsuranceTax>{insuranceTax}</InsuranceTax>
                <AnnualGross>{annualGross}</AnnualGross>
              </Premium>
              <Fees><AdminFee>{adminFee}</AdminFee></Fees>
              <Label>{label}</Label>
            </PricingResponse>
            """;
    }
}
