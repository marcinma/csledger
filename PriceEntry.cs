public sealed class PriceEntry
{
    public DateOnly Date { get; init; }
    public string FromCurrency { get; init; } = "";
    public decimal Rate { get; init; }
    public string ToCurrency { get; init; } = "";
}

