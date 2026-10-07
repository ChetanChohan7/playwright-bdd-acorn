using PricingValidationFramework.Core.Validation;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class XsdValidatorTests
{
    [Test]
    public void Preload_should_keep_the_compiled_schema_in_memory_for_subsequent_extraction()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cached-schema-{Guid.NewGuid():N}.xsd");
        File.Copy(new XsdFileResolver().Resolve("PricingComparisonFixture.xsd"), path);
        try
        {
            var validator = new XsdValidator();
            validator.Preload(path);
            File.Delete(path);

            var result = validator.ValidateAndExtractDecimals(CreateValidXml(), path);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.True);
                Assert.That(result.Document!.Values, Has.Count.EqualTo(4));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Extraction_should_reject_decimal_values_outside_the_supported_numeric_range()
    {
        var xml = CreateValidXml().Replace("412.30", "79228162514264337593543950336", StringComparison.Ordinal);
        var result = new XsdValidator().ValidateAndExtractDecimals(
            xml, new XsdFileResolver().Resolve("PricingComparisonFixture.xsd"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Document, Is.Null);
            Assert.That(string.Join(" | ", result.Errors), Does.Contain("AnnualNet"));
        });
    }

    [Test]
    public void Extraction_should_prohibit_external_dtd_resolution()
    {
        var xml = "<!DOCTYPE PricingResponse [<!ENTITY external SYSTEM 'file:///outside-the-trusted-directory'>]>" + CreateValidXml();
        var result = new XsdValidator().ValidateAndExtractDecimals(
            xml, new XsdFileResolver().Resolve("PricingComparisonFixture.xsd"));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_should_accept_xml_that_conforms_to_the_radar_schema()
    {
        var result = Validate(CreateValidXml());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        });
    }

    [Test]
    public void Validate_should_reject_well_formed_xml_that_violates_the_radar_schema()
    {
        var result = Validate("<PricingResponse xmlns=\"urn:pricing-comparison-fixture\"><Invalid>oops</Invalid></PricingResponse>");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(string.Join(" | ", result.Errors), Does.Contain("Invalid"));
        });
    }

    [Test]
    public void Validate_should_return_invalid_for_malformed_xml()
    {
        var result = Validate("<Response>");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
        });
    }

        [Test]
        public void Validate_and_extract_should_return_schema_decimal_elements_attributes_and_repeated_paths()
        {
                const string xml = """
                        <PricingResponse xmlns="urn:pricing-comparison-fixture" indexAdjustment="1.25">
                            <Premium>
                                <AnnualNet>10.00</AnnualNet>
                                <InsuranceTax>2.00</InsuranceTax>
                                <AnnualGross>12.00</AnnualGross>
                            </Premium>
                            <Fees>
                                <AdminFee>3.00</AdminFee>
                                <Surcharge>0.50</Surcharge>
                                <Surcharge>0.75</Surcharge>
                            </Fees>
                        </PricingResponse>
                        """;
                var xsdPath = new XsdFileResolver().Resolve("PricingComparisonFixture.xsd");

                var result = new XsdValidator().ValidateAndExtractDecimals(xml, xsdPath);

                Assert.Multiple(() =>
                {
                        Assert.That(result.IsValid, Is.True, string.Join(" | ", result.Errors));
                        Assert.That(result.Document!.Values["/PricingResponse/@indexAdjustment"], Is.EqualTo(1.25m));
                        Assert.That(result.Document.Values["/PricingResponse/Fees/Surcharge[0]"], Is.EqualTo(0.50m));
                        Assert.That(result.Document.Values["/PricingResponse/Fees/Surcharge[1]"], Is.EqualTo(0.75m));
                });
        }

    [Test]
    public void Validate_should_throw_for_an_invalid_schema()
    {
        var xsdPath = Path.Combine(Path.GetTempPath(), $"invalid-schema-{Guid.NewGuid():N}.xsd");
        File.WriteAllText(xsdPath, "<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"><xs:element>");

        try
        {
            Assert.Catch<Exception>(() => new XsdValidator().Validate("<Response />", xsdPath));
        }
        finally
        {
            File.Delete(xsdPath);
        }
    }

    [Test]
    public void Validate_should_throw_when_the_schema_file_is_missing()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-schema-{Guid.NewGuid():N}.xsd");

        Assert.Throws<FileNotFoundException>(() => new XsdValidator().Validate("<Response />", missingPath));
    }

    [Test]
    public void Validate_should_block_external_dtd_resolution()
    {
        var xml = "<!DOCTYPE PricingResponse [<!ENTITY external SYSTEM 'file:///outside-the-trusted-directory'>]><PricingResponse xmlns=\"urn:pricing-comparison-fixture\"><Label>&external;</Label></PricingResponse>";

        var result = Validate(xml);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(string.Join(" | ", result.Errors), Does.Contain("prohibit"));
        });
    }

    private static XsdValidationResult Validate(string responseXml)
    {
                var xsdPath = new XsdFileResolver().Resolve("PricingComparisonFixture.xsd");
        return new XsdValidator().Validate(responseXml, xsdPath);
    }

        private static string CreateValidXml()
        {
                return """
                        <PricingResponse xmlns="urn:pricing-comparison-fixture">
                            <Premium>
                                <AnnualNet>412.30</AnnualNet>
                                <InsuranceTax>49.48</InsuranceTax>
                                <AnnualGross>461.78</AnnualGross>
                            </Premium>
                            <Fees><AdminFee>25.00</AdminFee></Fees>
                            <Label>test</Label>
                        </PricingResponse>
                        """;
        }
}