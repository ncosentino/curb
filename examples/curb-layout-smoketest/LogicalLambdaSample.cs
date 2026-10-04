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
}
