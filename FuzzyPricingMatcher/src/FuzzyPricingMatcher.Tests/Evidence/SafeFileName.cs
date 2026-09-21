namespace FuzzyPricingMatcher.Tests.Evidence;

public static class SafeFileName
{
    public static string Convert(string value)
    {
        var withoutTraversal = value.Replace("..", "_");
        return new string(withoutTraversal.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
    }
}
