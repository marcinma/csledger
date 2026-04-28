public sealed class Ledger
{
    private readonly AccountNode _root = new("");
    private readonly List<PriceEntry> _prices;
    private readonly string _defaultCurrency;

    public Ledger(List<PriceEntry> prices, string defaultCurrency)
    {
        _prices = prices;
        _defaultCurrency = defaultCurrency.ToLowerInvariant();
    }

    public List<DayEntry> Days { get; } = new();

    public List<BudgetTransaction> MonthlyBudgetTransactions { get; } = new();

    public AccountNode Root => _root;

    public AccountNode GetOrCreateAccount(string path)
    {
        var current = _root;

        foreach (var part in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
            current = current.GetOrCreateChild(part);

        return current;
    }

    public AccountNode? GetAccount(string path)
    {
        var current = _root;

        foreach (var part in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current.GetChild(part);

            if (current == null)
                return null;
        }

        return current;
    }

    public IReadOnlyDictionary<string, decimal> Sum(string path)
    {
        return GetAccount(path)?.Sum() ?? new Dictionary<string, decimal>();
    }

    public IReadOnlyDictionary<string, decimal> TotalRaw(string path)
    {
        return GetAccount(path)?.TotalRaw() ?? new Dictionary<string, decimal>();
    }

    public decimal Total(string path)
    {
        var rawTotal = TotalRaw(path);
        var result = 0m;

        foreach (var item in rawTotal)
            result += ConvertCurrency(item.Value, item.Key, _defaultCurrency);

        return result;
    }
    public BudgetResult Budget(DateOnly date, BudgetPeriod period)
    {
        var range = BudgetDateRange.Create(date, period);
        var result = new BudgetResult
        {
            Start = range.Start,
            End = range.End,
            Currency = _defaultCurrency
        };

        static string GetRootAccount(string accountPath)
        {
            var index = accountPath.IndexOf(':');
            return index == -1 ? accountPath : accountPath[..index];
        }

        static bool IsIncomeAccount(string accountPath)
        {
            return GetRootAccount(accountPath).Equals("income", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsExpenseAccount(string accountPath)
        {
            return GetRootAccount(accountPath).Equals("expenses", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsBudgetAccount(string accountPath)
        {
            return IsIncomeAccount(accountPath) || IsExpenseAccount(accountPath);
        }

        static List<ResolvedPosting> ResolveBudgetPostings(BudgetTransaction transaction)
        {
            var resolved = new List<ResolvedPosting>();
            var transactionTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var posting in transaction.Postings)
            {
                if (posting.IsAssignment)
                    continue;

                if (posting.IsBalancingPosting)
                {
                    foreach (var item in transactionTotals)
                    {
                        resolved.Add(new ResolvedPosting
                        {
                            AccountPath = posting.AccountPath,
                            Amount = new Money(-item.Value, item.Key),
                            IsAssignment = false
                        });
                    }

                    transactionTotals.Clear();
                    continue;
                }

                if (posting.Amount == null)
                    continue;

                var money = posting.Amount.Value;

                if (!transactionTotals.ContainsKey(money.Currency))
                    transactionTotals[money.Currency] = 0;

                transactionTotals[money.Currency] += money.Amount;

                resolved.Add(new ResolvedPosting
                {
                    AccountPath = posting.AccountPath,
                    Amount = money,
                    IsAssignment = false
                });
            }

            if (transactionTotals.Values.Any(value => value != 0))
                throw new InvalidOperationException("Unbalanced monthly budget transaction.");

            return resolved;
        }

        foreach (var transaction in MonthlyBudgetTransactions)
        {
            if (transaction.Year != null && transaction.Year.Value != date.Year)
                continue;

            foreach (var posting in ResolveBudgetPostings(transaction))
            {
                if (!IsBudgetAccount(posting.AccountPath))
                    continue;

                var value = Math.Abs(ConvertCurrency(posting.Amount.Amount, posting.Amount.Currency, _defaultCurrency)) * range.MonthMultiplier;
                var account = result.GetOrCreateAccount(posting.AccountPath);

                if (IsIncomeAccount(posting.AccountPath))
                    account.PlannedIncome += value;

                if (IsExpenseAccount(posting.AccountPath))
                    account.PlannedExpense += value;
            }
        }

        var budgetAccounts = new HashSet<string>(
            result.Accounts.Keys,
            StringComparer.OrdinalIgnoreCase);

        foreach (var day in Days.Where(item => item.Date >= range.Start && item.Date <= range.End))
        {
            foreach (var posting in day.ResolvedPostings.Where(item => !item.IsAssignment))
            {
                if (!IsBudgetAccount(posting.AccountPath))
                    continue;

                var matchedBudgetAccount = budgetAccounts
                    .FirstOrDefault(account =>
                        posting.AccountPath.Equals(account, StringComparison.OrdinalIgnoreCase) ||
                        posting.AccountPath.StartsWith(account + ":", StringComparison.OrdinalIgnoreCase) ||
                        account.StartsWith(posting.AccountPath + ":", StringComparison.OrdinalIgnoreCase));

                if (matchedBudgetAccount == null)
                    matchedBudgetAccount = GetRootAccount(posting.AccountPath);

                var value = Math.Abs(ConvertCurrency(posting.Amount.Amount, posting.Amount.Currency, _defaultCurrency));
                var account = result.GetOrCreateAccount(matchedBudgetAccount);

                if (IsIncomeAccount(posting.AccountPath))
                    account.ActualIncome += value;

                if (IsExpenseAccount(posting.AccountPath))
                    account.ActualExpense += value;
            }
        }

        foreach (var account in result.Accounts.Values)
        {
            account.TotalIncome = account.PlannedIncome + account.ActualIncome;
            account.TotalExpense = account.PlannedExpense + account.ActualExpense;
            account.Net = account.TotalIncome - account.TotalExpense;
        }

        result.PlannedIncome = result.Accounts.Values.Sum(item => item.PlannedIncome);
        result.PlannedExpense = result.Accounts.Values.Sum(item => item.PlannedExpense);
        result.ActualIncome = result.Accounts.Values.Sum(item => item.ActualIncome);
        result.ActualExpense = result.Accounts.Values.Sum(item => item.ActualExpense);
        result.TotalIncome = result.PlannedIncome + result.ActualIncome;
        result.TotalExpense = result.PlannedExpense + result.ActualExpense;
        result.Net = result.TotalIncome - result.TotalExpense;

        return result;
    }
    private decimal ConvertCurrency(decimal amount, string fromCurrency, string toCurrency)
    {
        fromCurrency = fromCurrency.ToLowerInvariant();
        toCurrency = toCurrency.ToLowerInvariant();

        if (fromCurrency == toCurrency)
            return amount;

        var price = _prices
            .Where(item =>
                item.FromCurrency.Equals(fromCurrency, StringComparison.OrdinalIgnoreCase) &&
                item.ToCurrency.Equals(toCurrency, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Date)
            .FirstOrDefault();

        if (price == null)
            return 0;

        return amount * price.Rate;
    }
}

