using PricingValidationFramework.Core.Validation;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class XsdValidatorTests
{
    [Test]
    public void Validate_should_accept_xml_that_conforms_to_the_radar_schema()
    {
        var result = Validate("<Response><TotalAmount>100.00</TotalAmount></Response>");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        });
    }

    [Test]
    public void Validate_should_reject_well_formed_xml_that_violates_the_radar_schema()
    {
        var result = Validate("<Response><Invalid>oops</Invalid></Response>");

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
        var xml = "<!DOCTYPE Response [<!ENTITY external SYSTEM 'file:///outside-the-trusted-directory'>]><Response><TotalAmount>&external;</TotalAmount></Response>";

        var result = Validate(xml);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(string.Join(" | ", result.Errors), Does.Contain("prohibit"));
        });
    }

    private static XsdValidationResult Validate(string responseXml)
    {
        var xsdPath = new XsdFileResolver().Resolve("RadarSystemTest.xsd");
        return new XsdValidator().Validate(responseXml, xsdPath);
    }
}