using PricingValidationFramework.Core.Matching;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class FuzzyPricingMatcherTests
{
    private readonly FuzzyPricingMatcher matcher = new();

    [Test]
    public void Automatic_comparison_should_use_full_paths_and_inclusive_thresholds()
    {
        var expected = Document(
            ("/Pricing/Fees/Fee[0]/Amount", 25m),
            ("/Pricing/Fees/Fee[1]/Amount", 30m),
            ("/Pricing/Fees/Fee[1]/@tax", 2m));
        var actual = Document(
            ("/Pricing/Fees/Fee[1]/@tax", 2.02m),
            ("/Pricing/Fees/Fee[1]/Amount", 29.99m),
            ("/Pricing/Fees/Fee[0]/Amount", 25.01m));

        var result = matcher.Compare(expected, actual, -0.01m, 0.01m);

        Assert.Multiple(() =>
        {
            Assert.That(result.ComparedFieldCount, Is.EqualTo(3));
            Assert.That(result.PassedFieldCount, Is.EqualTo(2));
            Assert.That(result.FailedFieldCount, Is.EqualTo(1));
            Assert.That(result.Fields.Single(field => field.Expected == 30m).Actual, Is.EqualTo(29.99m));
        });
    }

    [Test]
    public void Automatic_comparison_should_report_fields_missing_on_either_side()
    {
        var result = matcher.Compare(
            Document(("/Pricing/ExpectedOnly", 1m), ("/Pricing/Shared", 2m)),
            Document(("/Pricing/ActualOnly", 3m), ("/Pricing/Shared", 2m)),
            -0.01m, 0.01m);

        Assert.Multiple(() =>
        {
            Assert.That(result.ComparedFieldCount, Is.EqualTo(3));
            Assert.That(result.FailedFieldCount, Is.EqualTo(2));
            Assert.That(result.Fields.Single(field => field.FieldKey == "/Pricing/ExpectedOnly").Actual, Is.Null);
            Assert.That(result.Fields.Single(field => field.FieldKey == "/Pricing/ActualOnly").Expected, Is.Null);
        });
    }

    [Test]
    public void Automatic_comparison_should_error_when_there_are_no_decimal_fields()
    {
        var result = matcher.Compare(Document(), Document(), -0.01m, 0.01m);

        Assert.That(result.Result, Is.EqualTo(ScenarioResult.Error));
    }

    [Test]
    public void Automatic_comparison_should_reject_an_inverted_threshold_range()
    {
        Assert.Throws<ArgumentException>(() => matcher.Compare(Document(), Document(), 0.01m, -0.01m));
    }

    [Test]
    public void Compare_should_apply_inclusive_range_per_decimal_path()
    {
        var expected = Document(
            ("/Pricing/Premium/AnnualNet", 412.30m),
            ("/Pricing/Fees/AdminFee", 25.00m));
        var actual = Document(
            ("/Pricing/Premium/AnnualNet", 412.31m),
            ("/Pricing/Fees/AdminFee", 30.00m));

        var result = matcher.Compare(expected, actual, -0.01m, 0.01m);

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(ScenarioResult.Fail));
            Assert.That(result.ComparedFieldCount, Is.EqualTo(2));
            Assert.That(result.PassedFieldCount, Is.EqualTo(1));
            Assert.That(result.FailedFieldCount, Is.EqualTo(1));
            Assert.That(result.Fields.Single(field => field.FieldKey == "/Pricing/Premium/AnnualNet").Delta, Is.EqualTo(0.01m));
            Assert.That(result.Fields.Single(field => field.FieldKey == "/Pricing/Fees/AdminFee").Delta, Is.EqualTo(5m));
        });
    }

    [Test]
    public void Compare_should_skip_optional_decimal_missing_on_both_sides()
    {
        var expected = Document(("/Pricing/Fees/AdminFee", 25m));
        var actual = Document(("/Pricing/Fees/AdminFee", 25m));

        var result = matcher.Compare(expected, actual, -0.01m, 0.01m);

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(ScenarioResult.Pass));
            Assert.That(result.ComparedFieldCount, Is.EqualTo(1));
            Assert.That(result.Fields.Single().FieldKey, Is.EqualTo("/Pricing/Fees/AdminFee"));
        });
    }

    [Test]
    public void Compare_should_fail_when_a_decimal_is_present_on_only_one_side()
    {
        var expected = Document(("/Pricing/Fees/AdminFee", 25m));
        var actual = Document(("/Pricing/Tax/Optional", 1m), ("/Pricing/Fees/AdminFee", 25m));

        var result = matcher.Compare(expected, actual, -0.01m, 0.01m);

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(ScenarioResult.Fail));
            Assert.That(result.FailedFieldCount, Is.EqualTo(1));
            Assert.That(result.Fields.Single(field => field.FieldKey == "/Pricing/Tax/Optional").Expected, Is.Null);
            Assert.That(result.Fields.Single(field => field.FieldKey == "/Pricing/Tax/Optional").Actual, Is.EqualTo(1m));
        });
    }

    [Test]
    public void Compare_should_pair_repeated_decimal_elements_and_attributes_by_index()
    {
        var expected = Document(
            ("/Pricing/Fees/Fee[0]/Amount", 25m),
            ("/Pricing/Fees/Fee[0]/@tax", 1m),
            ("/Pricing/Fees/Fee[1]/Amount", 30m),
            ("/Pricing/Fees/Fee[1]/@tax", 2m));
        var actual = Document(
            ("/Pricing/Fees/Fee[0]/Amount", 25.01m),
            ("/Pricing/Fees/Fee[0]/@tax", 1m),
            ("/Pricing/Fees/Fee[1]/Amount", 32m),
            ("/Pricing/Fees/Fee[1]/@tax", 2m));
        var result = matcher.Compare(expected, actual, -0.01m, 0.01m);

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(ScenarioResult.Fail));
            Assert.That(result.ComparedFieldCount, Is.EqualTo(4));
            Assert.That(result.PassedFieldCount, Is.EqualTo(3));
            Assert.That(result.FailedFieldCount, Is.EqualTo(1));
            Assert.That(result.Fields.Single(field => field.Expected == 30m).Actual, Is.EqualTo(32m));
        });
    }

    [Test]
    public void Compare_should_compare_decimal_paths_without_configuration()
    {
        var result = matcher.Compare(
            Document(("/Pricing/Unmapped", 1m)),
            Document(("/Pricing/Unmapped", 1m)),
            -0.01m,
            0.01m);

        Assert.That(result.Result, Is.EqualTo(ScenarioResult.Pass));
    }

    private static PricingDocument Document(params (string Path, decimal Value)[] values)
    {
        return new PricingDocument(values.Select(value => new PricingDecimalValue(value.Path, value.Value)));
    }
}
