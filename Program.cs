using System.Globalization;
var argsList = args.ToList();

var fileIndex = argsList.IndexOf("-f");

if (fileIndex == -1 || fileIndex + 1 >= argsList.Count)
{
    Console.WriteLine("Usage: dotnet run -f <file>");
    return;
}

var filePath = argsList[fileIndex + 1];

if (!File.Exists(filePath))
{
    Console.WriteLine($"File not found: {filePath}");
    return;
}

var parser = new LedgerParser();
var ledger = parser.Parse(filePath);

var assetsTotal = ledger.Total("assets:money");
//
// Chce wiedziec ile mam na koncie
// Wiedziec ile moge wydac i odlozyc
var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
culture.NumberFormat.NumberGroupSeparator = " ";
culture.NumberFormat.NumberDecimalSeparator = ".";

Console.WriteLine(assetsTotal.ToString("N2", culture));

//foreach (var item in assetsTotal)
//    Console.WriteLine($"{item.Value.ToString("N2", culture)} {item.Key.ToString()}");
var budget = ledger.Budget(new DateOnly(2026, 4, 14), BudgetPeriod.Monthly);

Console.WriteLine($"Income: {budget.TotalIncome}");
Console.WriteLine($"Expense: {budget.TotalExpense}");
Console.WriteLine($"Net: {budget.Net}");
