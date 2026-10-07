namespace PricingValidationFramework.Tests.Helpers.Validation;

using NUnit.Framework;

public sealed class ValidationSummary
{
    private readonly List<string> failures = new();

    public bool HasFailures => failures.Count > 0;
    public int FailureCount => failures.Count;

    public void AddFailure(string message)
    {
        failures.Add(message);
    }

    public void AssertNoFailures()
    {
        Assert.That(
            failures,
            Is.Empty,
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }
}