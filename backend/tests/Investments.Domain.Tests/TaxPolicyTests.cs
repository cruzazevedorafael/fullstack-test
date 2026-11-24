using Investments.Domain.Calculations;

namespace Investments.Domain.Tests;

public class TaxPolicyTests
{
    private static readonly DateOnly Created = new(2025, 1, 10);

    [Theory]
    [InlineData("2025-01-10", "0.225")]
    [InlineData("2026-01-09", "0.225")] // one day short of one year
    [InlineData("2026-01-10", "0.185")] // exactly one year is no longer "less than one year"
    [InlineData("2027-01-10", "0.185")] // exactly two years is not yet "older than two years"
    [InlineData("2027-01-11", "0.15")]
    public void GetRate_uses_the_investment_age_at_withdrawal(string withdrawal, string expected)
    {
        var rate = TaxPolicy.GetRate(InvestmentRules.Default, Created, DateOnly.Parse(withdrawal));

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), rate);
    }

    [Fact]
    public void GetRate_handles_investments_created_on_a_leap_day()
    {
        var created = new DateOnly(2024, 2, 29);

        Assert.Equal(0.225m, TaxPolicy.GetRate(InvestmentRules.Default, created, new DateOnly(2025, 2, 27)));
        Assert.Equal(0.185m, TaxPolicy.GetRate(InvestmentRules.Default, created, new DateOnly(2025, 2, 28)));
    }

    [Fact]
    public void GetRate_reads_rates_from_the_configured_rules()
    {
        var rules = InvestmentRules.Default with { TaxRateUnderOneYear = 0.30m };

        Assert.Equal(0.30m, TaxPolicy.GetRate(rules, Created, Created));
    }
}
