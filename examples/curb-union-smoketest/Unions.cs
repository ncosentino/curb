public class Unions
{
    private union Result(Started, Orphaned);
    private union Command(ParseCommand, CheckCommand, VerifyCommand, RunCommand, StateCommand, Usage);
    private union Message(Started, Orphaned)
    {
        public readonly int Code=>1 switch
        {
            1=>1,
            _=>0,
        };
    }

    private sealed record Started;
    private sealed record Orphaned;
    private sealed record ParseCommand;
    private sealed record CheckCommand;
    private sealed record VerifyCommand;
    private sealed record RunCommand;
    private sealed record StateCommand;
    private sealed record Usage;
}
