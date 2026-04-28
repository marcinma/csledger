public sealed class BudgetResult
{
    public DateOnly Start { get; init; }
    public DateOnly End { get; init; }
    public string Currency { get; init; } = "";

    public decimal PlannedIncome { get; set; }
    public decimal PlannedExpense { get; set; }
    public decimal ActualIncome { get; set; }
    public decimal ActualExpense { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Net { get; set; }

    public Dictionary<string, BudgetAccountResult> Accounts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public BudgetAccountResult GetOrCreateAccount(string accountPath)
    {
        if (!Accounts.TryGetValue(accountPath, out var account))
        {
            account = new BudgetAccountResult
            {
                AccountPath = accountPath
            };

            Accounts[accountPath] = account;
        }

        return account;
    }
}

