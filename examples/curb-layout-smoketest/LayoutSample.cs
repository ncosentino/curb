namespace LayoutSmoke;

public sealed class LayoutSample
{
    private async Task<Result<int>> ReadAsync(int number, CancellationToken ct) =>
        await TraceScope.RunAsync(async () => Outcome.CaptureAsync(async () => { await Task.Yield(); ct.ThrowIfCancellationRequested(); var doubled=number*2; return doubled; }));

    private readonly record struct Result<T>(T Value);

    private static class TraceScope
    {
        public static async Task<T> RunAsync<T>(Func<Task<Task<T>>> callback) => await await callback();
    }

    private static class Outcome
    {
        public static async Task<Result<T>> CaptureAsync<T>(Func<Task<T>> callback) => new(await callback());
    }
}
