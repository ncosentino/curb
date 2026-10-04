namespace LayoutSmoke;

internal static class LogicalLambdaSample
{
    private const int RequiredMinimumAllowedValue = 1;
    private const int RequiredPrimaryCategoryValue = 2;
    private const int RequiredSecondaryCategoryValue = 3;
    private const int ExcludedPermissionCategoryValue = 4;

    internal static bool Matches(IEnumerable<int> entries)
    {
        return entries.Any(entry => entry >= RequiredMinimumAllowedValue && (entry == RequiredPrimaryCategoryValue || entry == RequiredSecondaryCategoryValue) && entry != ExcludedPermissionCategoryValue);
    }

    internal static bool Invalid(IEnumerable<string>? entries)
    {
        if (entries is null || entries.Any(entry => string.IsNullOrWhiteSpace(entry) || entry.StartsWith("invalid-", StringComparison.OrdinalIgnoreCase) || entry.EndsWith("-rejected", StringComparison.OrdinalIgnoreCase))) { return true; }
        return false;
    }
}
