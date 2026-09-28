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