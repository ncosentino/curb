namespace LayoutSmoke;

internal static class ExpressionLambdaSample
{
    private const int RequiredMinimumAcceptedTextLength = 1;
    private const int RequiredSecondaryAcceptedTextLength = 2;

    internal static bool Invalid(IEnumerable<string> entries)
    {
        if (entries.Any(entry => entry.Length is not (RequiredMinimumAcceptedTextLength or RequiredSecondaryAcceptedTextLength))) { return true; }
        return false;
    }

    internal static IEnumerable<KeyValuePair<string, string>> Project(IEnumerable<string> entries)
    {
        return entries.Select(entry => new KeyValuePair<string, string>(entry.Substring(0, RequiredMinimumAcceptedTextLength), entry.ToUpperInvariant()));
    }
}
