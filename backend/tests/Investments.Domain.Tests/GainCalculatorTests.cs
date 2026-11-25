using Investments.Domain.Calculations;

namespace Investments.Domain.Tests;

public class GainCalculatorTests
{
    private const decimal MonthlyRate = 0.0052m;

    [Theory]
    [InlineData("2025-01-10", "2025-01-10", 0)] // same day
    [InlineData("2025-01-10", "2025-02-09", 0)] // one day before the first anniversary
    [InlineData("2025-01-10", "2025-02-10", 1)] // first anniversary
    [InlineData("2025-01-10", "2026-01-10", 12)]
    [InlineData("2025-01-31", "2025-02-27", 0)]
    [InlineData("2025-01-31", "2025-02-28", 1)] // day 31 pays on the last day of shorter months
    [InlineData("2025-01-31", "2025-03-30", 1)]
    [InlineData("2025-01-31", "2025-03-31", 2)]
    [InlineData("2024-02-29", "2025-02-28", 12)] // leap day
    [InlineData("2025-03-10", "2025-01-10", 0)] // end before start never yields months
    public void CompletedMonths_counts_only_full_monthly_anniversaries(string start, string end, int expected)
    {
        var months = GainCalculator.CompletedMonths(DateOnly.Parse(start), DateOnly.Parse(end));

        Assert.Equal(expected, months);
    }

    [Theory]
    [InlineData(0, "1000.00")]
    [InlineData(1, "1005.20")]
    [InlineData(2, "1010.43")] // compound: second month pays 5.23, not 5.20
    [InlineData(3, "1015.68")]
    [InlineData(12, "1064.22")]
    [InlineData(24, "1132.56")]
    public void CalculateBalance_applies_compound_gain(int months, string expected)
    {
        var balance = GainCalculator.CalculateBalance(1000m, MonthlyRate, months);

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), balance);
    }

    [Fact]
    public void CalculateBalance_rounds_only_the_final_result()
    {
        var balance = GainCalculator.CalculateBalance(2500.50m, MonthlyRate, 7);

        Assert.Equal(2592.95m, balance);
    }
}
