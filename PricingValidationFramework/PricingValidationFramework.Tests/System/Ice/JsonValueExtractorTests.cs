using PricingValidationFramework.Core.Extraction;

namespace PricingValidationFramework.Tests.System.Ice;

[TestFixture]
public class JsonValueExtractorTests
{
    private const string Response = """
        {
          "quote": {
            "wrttenoptions": [ { "hi": "Happy", "code": "A" } ],
            "grossPremiumAmountIncTax": { "amount": 512.30, "currency": "GBP" },
            "breakdown": [
              { "name": "cover", "Taxes": [ { "amount": 100, "code": "IPT", "rate": 12 }, { "amount": 20.50, "code": "VAT" } ] },
              { "name": "later", "taxes": [ { "amount": 999 } ] }
            ]
          }
        }
        """;

    [Test]
    public void Taxes_should_add_only_the_amount_fields_of_the_first_taxes_list()
    {
        Assert.That(new JsonValueExtractor().ExtractTaxes(Response), Is.EqualTo(120.50m), "the later taxes list (999) is never used");
    }

    [Test]
    public void A_single_tax_should_be_its_amount()
    {
        Assert.That(new JsonValueExtractor().ExtractTaxes("""{ "taxes": [ { "amount": 100, "code": "IPT" } ] }"""), Is.EqualTo(100m));
    }

    [Test]
    public void Premium_should_be_unchanged_by_the_taxes_lookup()
    {
        Assert.That(new JsonValueExtractor().ExtractPremium(Response), Is.EqualTo(512.30m));
    }

    [Test]
    public void Premium_excluding_taxes_should_be_the_premium_minus_the_taxes_total()
    {
        Assert.That(new JsonValueExtractor().ExtractPremiumExcludingTaxes(Response), Is.EqualTo(391.80m), "512.30 - (100 + 20.50)");
    }

    [Test]
    public void Premium_excluding_taxes_should_fail_when_either_value_is_missing()
    {
        var extractor = new JsonValueExtractor();

        Assert.Multiple(() =>
        {
            Assert.That(() => extractor.ExtractPremiumExcludingTaxes("""{ "wrttenoptions": [ { "hi": "happy" } ], "taxes": [ { "amount": 10 } ] }"""),
                Throws.InstanceOf<InvalidDataException>().With.Message.Contains("grossPremiumAmountIncTax"));
            Assert.That(() => extractor.ExtractPremiumExcludingTaxes("""{ "wrttenoptions": [ { "hi": "happy" } ], "grossPremiumAmountIncTax": { "amount": 100 } }"""),
                Throws.InstanceOf<InvalidDataException>().With.Message.Contains("taxes list"));
        });
    }

    [TestCase("""{ "wrttenoptions": [ { "hi": "sad" } ], "grossPremiumAmountIncTax": { "amount": 1 }, "taxes": [ { "amount": 0 } ] }""", "Observed hi value: \"sad\"")]
    [TestCase("""{ "wrttenoptions": [ { "hi": 1 } ], "grossPremiumAmountIncTax": { "amount": 1 }, "taxes": [ { "amount": 0 } ] }""", "Observed hi value: 1")]
    [TestCase("""{ "wrttenoptions": [ { "code": "A" } ], "grossPremiumAmountIncTax": { "amount": 1 }, "taxes": [ { "amount": 0 } ] }""", "<missing hi field>")]
    [TestCase("""{ "grossPremiumAmountIncTax": { "amount": 1 }, "taxes": [ { "amount": 0 } ] }""", "<missing hi field>")]
    public void Premium_and_premium_excluding_taxes_should_need_a_happy_status(string json, string observed)
    {
        var extractor = new JsonValueExtractor();

        Assert.Multiple(() =>
        {
            Assert.That(() => extractor.ExtractPremium(json),
                Throws.InstanceOf<InvalidDataException>().With.Message.Contains("hi status \"happy\"").And.Message.Contains(observed));
            Assert.That(() => extractor.ExtractPremiumExcludingTaxes(json),
                Throws.InstanceOf<InvalidDataException>().With.Message.Contains("hi status \"happy\""));
            Assert.That(extractor.ExtractTaxes(json), Is.Zero, "taxes alone don't need the status");
        });
    }

    [Test]
    public void A_first_taxes_value_that_is_not_a_list_should_be_rejected_even_if_a_later_one_is()
    {
        var json = """{ "a": { "taxes": "included" }, "b": { "taxes": [ { "amount": 100 } ] } }""";

        Assert.That(() => new JsonValueExtractor().ExtractTaxes(json),
            Throws.InstanceOf<InvalidDataException>().With.Message.Contains("first taxes value is not a list"));
    }

    [TestCase("""{ "quote": { "grossPremiumAmountIncTax": { "amount": 1 } } }""", "does not contain a taxes list")]
    [TestCase("""{ "taxes": [] }""", "at least one entry")]
    [TestCase("""{ "taxes": [ { "amount": 100 }, { "amount": "20" } ] }""", "Entry 2")]
    [TestCase("""{ "taxes": [ { "amount": 100 }, { "code": "VAT" } ] }""", "Entry 2")]
    public void A_response_without_a_complete_taxes_list_should_be_rejected(string json, string message)
    {
        Assert.That(() => new JsonValueExtractor().ExtractTaxes(json),
            Throws.InstanceOf<InvalidDataException>().With.Message.Contains(message));
    }
}
