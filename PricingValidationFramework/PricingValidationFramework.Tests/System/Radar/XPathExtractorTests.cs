using PricingValidationFramework.Core.Extraction;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class XPathExtractorTests
{
    [TestCase("<Response><Amount>12.50</Amount></Response>", "/Response/Amount", true)]
    [TestCase("<Response amount=\"12.50\"/>", "/Response/@amount", true)]
    [TestCase("<Response><Amount>1</Amount><Amount>12.50</Amount></Response>", "/Response/Amount[2]", true)]
    [TestCase("<Response/>", "/Response/Amount", false)]
    [TestCase("<Response><Amount>1</Amount><Amount>2</Amount></Response>", "/Response/Amount", false)]
    [TestCase("<Response><Amount>invalid</Amount></Response>", "/Response/Amount", false)]
    [TestCase("<Response><Amount>79228162514264337593543950336</Amount></Response>", "/Response/Amount", false)]
    [TestCase("<Response><Amount>1,50</Amount></Response>", "/Response/Amount", false)]
    [TestCase("<Response/>", "count(/Response)", false)]
    [TestCase("<Response/>", "[", false)]
    [TestCase("<Response/>", "/p:Response", false)]
    [TestCase("<Response><Amount>12.50</Amount></Response>", "/Response/Amount/text()", false)]
    [TestCase("<!DOCTYPE Response [<!ENTITY external SYSTEM 'file:///not-accessed'>]><Response/>", "/Response", false)]
    [TestCase("<Response", "/Response/Amount", false)]
    public void Extract_should_handle_exact_node_selection_and_report_invalid_values(string xml, string path, bool valid)
    {
        var result = new XmlValueExtractor().Extract(xml, new Dictionary<string, string> { ["premium"] = path });
        Assert.That(result.IsValid, Is.EqualTo(valid));
        if (valid)
        {
            Assert.That(result.Document!.Values["premium"], Is.EqualTo(12.50m));
            Assert.That(result.Document.Paths["premium"], Is.EqualTo(path));
        }
        else
        {
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors, Is.Not.Empty);
        }
    }

    [Test]
    public void Extract_should_collect_all_field_errors_without_default_values()
    {
        var result = new XmlValueExtractor().Extract("<Response/>", new Dictionary<string, string>
        {
            ["net"] = "/Response/Net",
            ["tax"] = "/Response/Tax"
        });
        Assert.That(result.Errors, Has.Count.EqualTo(2));
        Assert.That(result.Document, Is.Null);
    }
}
