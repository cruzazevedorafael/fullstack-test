using System.ComponentModel.DataAnnotations;
using Investments.Domain.Calculations;
using Investments.Domain.Entities;

namespace Investments.Api.Contracts;

public sealed record CreateInvestmentRequest
{
    [Required]
    public Guid? OwnerId { get; init; }

    /// <summary>Invested amount: greater than zero, up to two decimal places.</summary>
    [Required]
    public decimal? Amount { get; init; }

    /// <summary>Today or a past date (yyyy-MM-dd).</summary>
    [Required]
    public DateOnly? CreationDate { get; init; }
}

public sealed record WithdrawRequest
{
    /// <summary>From the creation date up to today (yyyy-MM-dd).</summary>
    [Required]
    public DateOnly? Date { get; init; }
}

public sealed record ListInvestmentsRequest : PageRequest
{
    /// <summary>When informed, returns only the investments of this person.</summary>
    public Guid? OwnerId { get; init; }
}

public sealed record WithdrawalResponse(
    DateOnly Date,
    decimal GrossAmount,
    decimal Gains,
    decimal TaxRate,
    decimal TaxAmount,
    decimal NetAmount)
{
    public static WithdrawalResponse From(Withdrawal w) =>
        new(w.Date, w.GrossAmount, w.Gains, w.TaxRate, w.TaxAmount, w.NetAmount);
}

/// <param name="Gains">Gains accumulated so far, or up to the withdrawal date.</param>
/// <param name="ExpectedBalance">Initial amount plus gains, before taxes.</param>
/// <param name="Withdrawal">Values of the withdrawal, or null while the investment is active.</param>
public sealed record InvestmentResponse(
    Guid Id,
    OwnerResponse Owner,
    DateOnly CreationDate,
    decimal InitialAmount,
    decimal Gains,
    decimal ExpectedBalance,
    InvestmentStatus Status,
    WithdrawalResponse? Withdrawal)
{
    public static InvestmentResponse From(Investment investment, DateOnly today, InvestmentRules rules)
    {
        var balance = investment.GetBalance(today, rules);

        return new InvestmentResponse(
            investment.Id,
            OwnerResponse.From(investment.Owner!),
            investment.CreationDate,
            investment.InitialAmount,
            balance.Gains,
            balance.CurrentBalance,
            investment.Status,
            investment.Withdrawal is null ? null : WithdrawalResponse.From(investment.Withdrawal));
    }
}
