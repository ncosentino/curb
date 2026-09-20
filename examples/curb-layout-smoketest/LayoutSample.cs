namespace LayoutSmoke;

public sealed class LayoutSample
{
    private const int RequiredCategoryIdentifierForCondition = 1;
    private const int ExcludedSegmentIdentifierForCondition = 2;

    private async Task<Result<int>> ReadAsync(int number, CancellationToken ct) =>
        await TraceScope.RunAsync(async () => Outcome.CaptureAsync(async () => { await Task.Yield(); ct.ThrowIfCancellationRequested(); var text = """
            content
                indented
            """; var doubled=number*2+text.Length; if (number == RequiredCategoryIdentifierForCondition && number != ExcludedSegmentIdentifierForCondition && !ct.IsCancellationRequested) { doubled++; } return doubled; }));

    private readonly record struct Result<T>(T Value);

    private static string Property { get; } = """
        property
        """;

    private static string Returned()
    {
        return """
            returned
            """;
    }

    private static string Arrow() => """
        arrow
        """;

    private static string Named() => Consume(text: """
            named
            """);

    private static string Conditional(bool flag) => flag ? """
            first
            """ : """
            second
            """;

    private static string Interpolated(string name) => $$""""
        literal """ and {{name}}
        """";

    private static ReadOnlySpan<byte> Utf8() => """
        bytes
        """u8;

    private static string Consume(string text) => text;

    private static class TraceScope
    {
        public static async Task<T> RunAsync<T>(Func<Task<Task<T>>> callback) => await await callback();
    }

    private static class Outcome
    {
        public static async Task<Result<T>> CaptureAsync<T>(Func<Task<T>> callback) => new(await callback());
    }
}
