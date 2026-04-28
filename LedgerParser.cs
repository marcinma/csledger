using System.Globalization;
using System.Text.RegularExpressions;

public sealed class LedgerParser
{
    public string DefaultCurrency { get; set; } = "pln";

    private readonly List<PriceEntry> _prices = new();

    public Ledger Parse(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Input file was not found.", path);

        _prices.Clear();

        var ledger = new Ledger(_prices, DefaultCurrency);
        DayEntry? currentDay = null;
        BudgetTransaction? currentBudget = null;

        var lineParsers = new Dictionary<string, Func<string, Posting?>>(StringComparer.OrdinalIgnoreCase)
        {
            [";"] = _ => null,
            ["#"] = _ => null,
            ["P"] = line =>
            {
                ParsePrice(line);
                return null;
            }
        };

        foreach (var rawLine in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            var line = rawLine.TrimEnd();
            var trimmedLine = line.Trim();

            if (Regex.IsMatch(trimmedLine, @"^\d{4}-\d{2}-\d{2}(?:\s+.*)?$"))
            {
                currentBudget = null;
                currentDay = ParseDay(trimmedLine);
                ledger.Days.Add(currentDay);
                continue;
            }

            if (trimmedLine.StartsWith("~ monthly", StringComparison.OrdinalIgnoreCase))
            {
                currentDay = null;

                var yearMatch = Regex.Match(trimmedLine, @"\bin\s+(\d{4})\b", RegexOptions.IgnoreCase);

                currentBudget = new BudgetTransaction
                {
                    Year = yearMatch.Success ? int.Parse(yearMatch.Groups[1].Value) : null
                };

                ledger.MonthlyBudgetTransactions.Add(currentBudget);
                continue;
            }

            var lineParser = lineParsers
                .FirstOrDefault(item => trimmedLine.StartsWith(item.Key, StringComparison.OrdinalIgnoreCase))
                .Value;

            if (lineParser != null)
            {
                var parsedPosting = lineParser(trimmedLine);

                if (parsedPosting != null && currentDay != null)
                    currentDay.Postings.Add(parsedPosting);

                continue;
            }

            if (line.StartsWith('\t') || line.StartsWith("    "))
            {
                var posting = ParsePosting(trimmedLine);

                if (currentBudget != null)
                {
                    if (posting != null)
                        currentBudget.Postings.Add(posting);

                    continue;
                }

                if (currentDay != null)
                {
                    if (posting != null)
                        currentDay.Postings.Add(posting);

                    continue;
                }

                Console.WriteLine($"Skipping posting outside section: {line}");
                continue;
            }

            Console.WriteLine($"Skipping unsupported line: {line}");
        }

        ApplyPostings(ledger);
        ApplyBudgetTransactions(ledger);

        return ledger;
    }

    private void ParsePrice(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 5 || !parts[0].Equals("P", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Skipping invalid price line: {line}");
            return;
        }

        if (!DateOnly.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            Console.WriteLine($"Skipping invalid price date: {line}");
            return;
        }

        if (!decimal.TryParse(parts[3], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate))
        {
            Console.WriteLine($"Skipping invalid price rate: {line}");
            return;
        }

        _prices.Add(new PriceEntry
        {
            Date = date,
            FromCurrency = parts[2].ToLowerInvariant(),
            Rate = rate,
            ToCurrency = parts[4].ToLowerInvariant()
        });
    }

    private static DayEntry ParseDay(string line)
    {
        var match = Regex.Match(line, @"^(\d{4}-\d{2}-\d{2})(?:\s+(.*))?$");

        if (!match.Success)
            throw new FormatException($"Invalid day line: {line}");

        return new DayEntry
        {
            Date = DateOnly.ParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Comment = match.Groups[2].Value.Trim()
        };
    }

    private static Posting ParsePosting(string line)
    {
        line = line.Trim();

        if (string.IsNullOrWhiteSpace(line))
            throw new FormatException("Empty posting line.");

        var split = Regex.Match(line, @"^(\S+)(?:\s+(.*))?$");

        if (!split.Success)
            throw new FormatException($"Invalid posting line: {line}");

        var accountPath = split.Groups[1].Value.Trim();
        var rest = split.Groups[2].Success ? split.Groups[2].Value.Trim() : "";

        if (string.IsNullOrWhiteSpace(rest) || rest == ".")
        {
            return new Posting
            {
                AccountPath = accountPath,
                IsBalancingPosting = true
            };
        }

        var isAssignment = false;

        if (rest.StartsWith("="))
        {
            isAssignment = true;
            rest = rest[1..].Trim();
        }

        var amount = ParseMoney(rest);

        return new Posting
        {
            AccountPath = accountPath,
            IsAssignment = isAssignment,
            Amount = amount
        };
    }

    private static Money ParseMoney(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
            throw new FormatException($"Invalid money value: {text}");

        var currency = parts[^1].ToLowerInvariant();
        var numberText = string.Concat(parts.Take(parts.Length - 1));

        if (!decimal.TryParse(
                numberText,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new FormatException($"Invalid amount: {text}");
        }

        return new Money(value, currency);
    }

    private static void ApplyPostings(Ledger ledger)
    {
        foreach (var day in ledger.Days)
        {
            var transactionTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var posting in day.Postings)
            {
                var account = ledger.GetOrCreateAccount(posting.AccountPath);

                if (posting.IsBalancingPosting)
                {
                    foreach (var item in transactionTotals)
                    {
                        var money = new Money(-item.Value, item.Key);
                        account.AddAmount(money);
                    }

                    transactionTotals.Clear();
                    continue;
                }

                if (posting.Amount == null)
                    continue;

                var moneyValue = posting.Amount.Value;

                if (posting.IsAssignment)
                {
                    var currentTotal = account.TotalRaw();
                    currentTotal.TryGetValue(moneyValue.Currency, out var currentAmount);

                    var difference = moneyValue.Amount - currentAmount;

                    account.SetAmount(moneyValue);

                    if (!transactionTotals.ContainsKey(moneyValue.Currency))
                        transactionTotals[moneyValue.Currency] = 0;

                    transactionTotals[moneyValue.Currency] += difference;

                    day.ResolvedPostings.Add(new ResolvedPosting
                    {
                        AccountPath = posting.AccountPath,
                        Amount = new Money(difference, moneyValue.Currency),
                        IsAssignment = true
                    });

                    continue;
                }

                account.AddAmount(moneyValue);

                if (!transactionTotals.ContainsKey(moneyValue.Currency))
                    transactionTotals[moneyValue.Currency] = 0;

                transactionTotals[moneyValue.Currency] += moneyValue.Amount;

                day.ResolvedPostings.Add(new ResolvedPosting
                {
                    AccountPath = posting.AccountPath,
                    Amount = moneyValue,
                    IsAssignment = false
                });
            }

            if (transactionTotals.Values.Any(value => value != 0))
                throw new InvalidOperationException($"Unbalanced transaction on {day.Date:yyyy-MM-dd}.");
        }
    }
    private static void ApplyBudgetTransactions(Ledger ledger)
    {
        foreach (var transaction in ledger.MonthlyBudgetTransactions)
        {
            transaction.ResolvedPostings.Clear();

            var transactionTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var posting in transaction.Postings)
            {
                if (posting.IsAssignment)
                    continue;

                if (posting.IsBalancingPosting)
                {
                    foreach (var item in transactionTotals)
                    {
                        var money = new Money(-item.Value, item.Key);

                        transaction.ResolvedPostings.Add(new ResolvedPosting
                        {
                            AccountPath = posting.AccountPath,
                            Amount = money,
                            IsAssignment = false
                        });
                    }

                    transactionTotals.Clear();
                    continue;
                }

                if (posting.Amount == null)
                    continue;

                var moneyValue = posting.Amount.Value;

                if (!transactionTotals.ContainsKey(moneyValue.Currency))
                    transactionTotals[moneyValue.Currency] = 0;

                transactionTotals[moneyValue.Currency] += moneyValue.Amount;

                transaction.ResolvedPostings.Add(new ResolvedPosting
                {
                    AccountPath = posting.AccountPath,
                    Amount = moneyValue,
                    IsAssignment = false
                });
            }

            if (transactionTotals.Values.Any(value => value != 0))
                throw new InvalidOperationException("Unbalanced monthly budget transaction.");
        }
    }
}
