public sealed class ResolvedPosting
{
    public string AccountPath { get; init; } = "";
    public Money Amount { get; init; }
    public bool IsAssignment { get; init; }
}

