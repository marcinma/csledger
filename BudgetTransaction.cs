public sealed class BudgetTransaction
{
    public int? Year { get; init; }
    public List<Posting> Postings { get; } = new();
    public List<ResolvedPosting> ResolvedPostings { get; } = new();
}

