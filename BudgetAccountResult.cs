public sealed class BudgetAccountResult
{
    public string AccountPath { get; init; } = "";
    public decimal PlannedIncome { get; set; }
    public decimal PlannedExpense { get; set; }
    public decimal ActualIncome { get; set; }
    public decimal ActualExpense { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Net { get; set; }
}

