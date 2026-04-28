public sealed class DayEntry
{
    public DateOnly Date { get; init; }
    public string Comment { get; init; } = "";
    public List<Posting> Postings { get; } = new();
    public List<ResolvedPosting> ResolvedPostings { get; } = new();
}

