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

public sealed record SimulationRequest
{
    /// <summary>Amount to invest: greater than zero, up to two decimal places.</summary>
    [Required]
    public decimal? Amount { get; init; }

    /// <summary>Today or a past date (yyyy-MM-dd).</summary>
    [Required]
    public DateOnly? CreationDate { get; init; }

    /// <summary>Months between the creation and the simulated withdrawal (1 to 600).</summary>
    [Required]
    public int? Months { get; init; }
}

public enum InvestmentSort
{
    CreationDate,
    Amount,
    Owner,
}

public enum SortDirection
{
    Asc,
    Desc,
}

public sealed record ListInvestmentsRequest : PageRequest
{
    /// <summary>When informed, returns only the investments of this person.</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>When informed, returns only the investments with this status.</summary>
    public InvestmentStatus? Status { get; init; }

    /// <summary>Field used to sort. The balance isn't an option because it is calculated, not stored.</summary>
    public InvestmentSort SortBy { get; init; } = InvestmentSort.CreationDate;

    public SortDirection SortDirection { get; init; } = SortDirection.Desc;
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
public record InvestmentResponse(
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

/// <param name="MonthlyGainRate">Rate paid every month, e.g. 0.0052 for 0.52%.</param>
/// <param name="CompletedMonths">Monthly gains already paid.</param>
/// <param name="WithdrawalToday">Values of a withdrawal made today.</param>
public sealed record AccrualResponse(
    decimal MonthlyGainRate,
    int CompletedMonths,
    DateOnly NextGainDate,
    decimal NextGainAmount,
    WithdrawalResponse WithdrawalToday)
{
    public static AccrualResponse From(InvestmentAccrual a) =>
        new(a.MonthlyGainRate, a.CompletedMonths, a.NextGainDate, a.NextGainAmount, WithdrawalResponse.From(a.WithdrawalToday));
}

/// <summary>A single investment, with more details than in the list.</summary>
public sealed record InvestmentDetailsResponse : InvestmentResponse
{
    private InvestmentDetailsResponse(InvestmentResponse summary)
        : base(summary)
    {
    }

    /// <summary>Null once the investment is withdrawn.</summary>
    public AccrualResponse? Accrual { get; private init; }

    public static new InvestmentDetailsResponse From(Investment investment, DateOnly today, InvestmentRules rules) =>
        new(InvestmentResponse.From(investment, today, rules))
        {
            Accrual = investment.GetAccrual(today, rules) is { } accrual ? AccrualResponse.From(accrual) : null,
        };
}

/// <param name="Projected">True for a future payment.</param>
public sealed record GrowthPointResponse(DateOnly Date, decimal Balance, bool Projected);
