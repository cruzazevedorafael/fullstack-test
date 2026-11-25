namespace Investments.Domain.Calculations;

/// <summary>Compound gain math. Pure functions, no state.</summary>
public static class GainCalculator
{
    /// <summary>
    /// Number of monthly anniversaries reached between <paramref name="start"/> and <paramref name="end"/>.
    /// The gain is paid on the same day of the month as the creation date; when that day does not
    /// exist in a month (e.g. the 31st in February) it is paid on the last day of that month.
    /// </summary>
    public static int CompletedMonths(DateOnly start, DateOnly end)
    {
        if (end <= start)
        {
            return 0;
        }

        var months = (end.Year - start.Year) * 12 + (end.Month - start.Month);

        // DateOnly.AddMonths clamps to the last day of the month, which gives the anniversary rule above.
        if (start.AddMonths(months) > end)
        {
            months--;
        }

        return months;
    }

    /// <summary>
    /// <c>amount × (1 + rate)^months</c>, computed at full decimal precision and rounded to cents
    /// only at the end so rounding errors do not accumulate month after month.
    /// </summary>
    public static decimal CalculateBalance(decimal amount, decimal monthlyRate, int months)
    {
        var balance = amount;
        var factor = 1m + monthlyRate;

        for (var i = 0; i < months; i++)
        {
            balance *= factor;
        }

        return Money.Round(balance);
    }
}
