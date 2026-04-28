public sealed class BudgetDateRange
{
    public DateOnly Start { get; init; }
    public DateOnly End { get; init; }
    public int MonthMultiplier { get; init; }

    public static BudgetDateRange Create(DateOnly date, BudgetPeriod period)
    {
        return period switch
        {
            BudgetPeriod.Daily => new BudgetDateRange
            {
                Start = date,
                End = date,
                MonthMultiplier = 0
            },
            BudgetPeriod.Monthly => new BudgetDateRange
            {
                Start = new DateOnly(date.Year, date.Month, 1),
                End = new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)),
                MonthMultiplier = 1
            },
            BudgetPeriod.Quarterly => CreateQuarter(date),
            BudgetPeriod.Yearly => new BudgetDateRange
            {
                Start = new DateOnly(date.Year, 1, 1),
                End = new DateOnly(date.Year, 12, 31),
                MonthMultiplier = 12
            },
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, null)
        };
    }

    private static BudgetDateRange CreateQuarter(DateOnly date)
    {
        var startMonth = ((date.Month - 1) / 3) * 3 + 1;
        var endMonth = startMonth + 2;

        return new BudgetDateRange
        {
            Start = new DateOnly(date.Year, startMonth, 1),
            End = new DateOnly(date.Year, endMonth, DateTime.DaysInMonth(date.Year, endMonth)),
            MonthMultiplier = 3
        };
    }
}

