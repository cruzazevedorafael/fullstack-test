using Investments.Domain.Calculations;
using Investments.Domain.Entities;
using Investments.Domain.Exceptions;

namespace Investments.Domain.Tests;

public class InvestmentTests
{
    private static readonly InvestmentRules Rules = InvestmentRules.Default;
    private static readonly DateOnly Today = new(2026, 6, 15);
    private static readonly DateOnly Created = new(2025, 1, 10);
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static Investment NewInvestment(decimal amount = 1000m) =>
        Investment.Create(OwnerId, amount, Created, Today);

    // ---------- creation ----------

    [Fact]
    public void Create_starts_active_with_the_given_data()
    {
        var investment = NewInvestment();

        Assert.Equal(OwnerId, investment.OwnerId);
        Assert.Equal(1000m, investment.InitialAmount);
        Assert.Equal(Created, investment.CreationDate);
        Assert.Equal(InvestmentStatus.Active, investment.Status);
        Assert.Null(investment.Withdrawal);
    }

    [Fact]
    public void Create_accepts_today_as_creation_date()
    {
        var investment = Investment.Create(OwnerId, 1000m, Today, Today);

        Assert.Equal(Today, investment.CreationDate);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("0")]
    public void Create_rejects_non_positive_amounts(string amount)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Investment.Create(OwnerId, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Created, Today));

        Assert.Equal(DomainErrorKind.Validation, ex.Kind);
    }

    [Fact]
    public void Create_rejects_more_than_two_decimal_places()
    {
        Assert.Throws<DomainException>(() => Investment.Create(OwnerId, 10.001m, Created, Today));
    }

    [Fact]
    public void Create_rejects_future_dates()
    {
        Assert.Throws<DomainException>(() => Investment.Create(OwnerId, 1000m, Today.AddDays(1), Today));
    }

    [Fact]
    public void Create_requires_an_owner()
    {
        Assert.Throws<DomainException>(() => Investment.Create(Guid.Empty, 1000m, Created, Today));
    }

    // ---------- balance ----------

    [Fact]
    public void GetBalance_of_an_active_investment_includes_compound_gains()
    {
        var investment = NewInvestment();

        var balance = investment.GetBalance(new DateOnly(2026, 1, 10), Rules);

        Assert.Equal(1000m, balance.InitialAmount);
        Assert.Equal(1064.22m, balance.CurrentBalance);
        Assert.Equal(64.22m, balance.Gains);
    }

    [Fact]
    public void GetBalance_before_any_anniversary_has_no_gains()
    {
        var balance = NewInvestment().GetBalance(Created, Rules);

        Assert.Equal(1000m, balance.CurrentBalance);
        Assert.Equal(0m, balance.Gains);
    }

    [Fact]
    public void GetBalance_of_a_withdrawn_investment_is_frozen_at_the_withdrawal_date()
    {
        var investment = NewInvestment();
        investment.Withdraw(new DateOnly(2026, 1, 10), Today, Rules);

        var muchLater = investment.GetBalance(new DateOnly(2030, 1, 1), Rules);

        Assert.Equal(1064.22m, muchLater.CurrentBalance);
        Assert.Equal(64.22m, muchLater.Gains);
    }

    // ---------- withdrawal ----------

    [Theory]
    [InlineData("2025-07-10", "1031.61", "31.61", "0.225", "7.11", "1024.50")]  // under one year
    [InlineData("2026-01-10", "1064.22", "64.22", "0.185", "11.88", "1052.34")] // exactly one year
    [InlineData("2027-01-10", "1132.56", "132.56", "0.185", "24.52", "1108.04")] // exactly two years
    [InlineData("2027-02-10", "1138.44", "138.44", "0.15", "20.77", "1117.67")]  // over two years
    public void QuoteWithdrawal_taxes_only_the_gains(
        string date, string gross, string gains, string rate, string tax, string net)
    {
        var today = new DateOnly(2027, 6, 1);
        var quote = NewInvestment().QuoteWithdrawal(DateOnly.Parse(date), today, Rules);

        Assert.Equal(Dec(gross), quote.GrossAmount);
        Assert.Equal(Dec(gains), quote.Gains);
        Assert.Equal(Dec(rate), quote.TaxRate);
        Assert.Equal(Dec(tax), quote.TaxAmount);
        Assert.Equal(Dec(net), quote.NetAmount);
    }

    [Fact]
    public void QuoteWithdrawal_does_not_change_the_investment()
    {
        var investment = NewInvestment();

        investment.QuoteWithdrawal(new DateOnly(2026, 1, 10), Today, Rules);

        Assert.Equal(InvestmentStatus.Active, investment.Status);
        Assert.Null(investment.Withdrawal);
    }

    [Fact]
    public void Withdraw_marks_the_investment_as_withdrawn_and_stores_the_values()
    {
        var investment = NewInvestment();

        var result = investment.Withdraw(new DateOnly(2026, 1, 10), Today, Rules);

        Assert.Equal(InvestmentStatus.Withdrawn, investment.Status);
        Assert.Equal(result, investment.Withdrawal);
        Assert.Equal(new DateOnly(2026, 1, 10), result.Date);
        Assert.Equal(1052.34m, result.NetAmount);
    }

    [Fact]
    public void Withdraw_on_the_creation_date_is_allowed()
    {
        var result = NewInvestment().Withdraw(Created, Today, Rules);

        Assert.Equal(1000m, result.NetAmount);
        Assert.Equal(0m, result.TaxAmount);
    }

    [Fact]
    public void Withdraw_rejects_dates_before_the_creation()
    {
        var ex = Assert.Throws<DomainException>(() =>
            NewInvestment().Withdraw(Created.AddDays(-1), Today, Rules));

        Assert.Equal(DomainErrorKind.Validation, ex.Kind);
    }

    [Fact]
    public void Withdraw_rejects_future_dates()
    {
        Assert.Throws<DomainException>(() => NewInvestment().Withdraw(Today.AddDays(1), Today, Rules));
    }

    [Fact]
    public void Withdraw_twice_is_a_conflict()
    {
        var investment = NewInvestment();
        investment.Withdraw(new DateOnly(2026, 1, 10), Today, Rules);

        var ex = Assert.Throws<DomainException>(() => investment.Withdraw(Today, Today, Rules));

        Assert.Equal(DomainErrorKind.Conflict, ex.Kind);
    }

    private static decimal Dec(string value) =>
        decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
