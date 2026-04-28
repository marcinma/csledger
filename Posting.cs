public sealed class Posting
{
    public string AccountPath { get; init; } = "";
    public bool IsAssignment { get; init; }
    public bool IsBalancingPosting { get; init; }
    public Money? Amount { get; init; }
}

