using Investments.Domain.Calculations;
using Investments.Domain.Exceptions;

namespace Investments.Domain.Entities;

public enum InvestmentStatus
{
    Active,
    Withdrawn,
}

/// <summary>Balance of an investment on a given date.</summary>
public sealed record InvestmentBalance(DateOnly AsOf, decimal InitialAmount, decimal Gains, decimal CurrentBalance);

/// <summary>Values of a (simulated or executed) full withdrawal.</summary>
public sealed record Withdrawal(
    DateOnly Date,
    decimal GrossAmount,
    decimal Gains,
    decimal TaxRate,
    decimal TaxAmount,
    decimal NetAmount);

/// <summary>
/// An amount invested by an owner. Only the initial data is stored while it is active:
/// the balance changes every month, so it is always calculated on demand. Once withdrawn,
/// the withdrawal values are stored and the balance is frozen.
/// </summary>
public sealed class Investment
{
    /// <summary>Upper bound that keeps the compound gain inside the range of <see cref="decimal"/>.</summary>
    public const decimal MaxAmount = 999_999_999_999.99m;

    /// <summary>Oldest accepted creation date, for the same reason.</summary>
    public static readonly DateOnly MinCreationDate = new(1900, 1, 1);

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Owner? Owner { get; private set; }
    public decimal InitialAmount { get; private set; }
    public DateOnly CreationDate { get; private set; }
    public InvestmentStatus Status { get; private set; }
    public Withdrawal? Withdrawal { get; private set; }

    private Investment()
    {
        // Required by EF Core.
    }

    /// <param name="today">Current date, injected so the rule can be tested deterministically.</param>
    public static Investment Create(Guid ownerId, decimal amount, DateOnly creationDate, DateOnly today)
    {
        if (ownerId == Guid.Empty)
        {
            throw new DomainException("investment.owner_required", "An owner is required.");
        }

        if (amount <= 0)
        {
            throw new DomainException("investment.amount_not_positive", "The amount must be greater than zero.");
        }

        if (amount > MaxAmount)
        {
            throw new DomainException("investment.amount_too_large", "The amount is above the supported limit.");
        }

        if (!Money.HasAtMostTwoDecimals(amount))
        {
            throw new DomainException("investment.amount_precision", "The amount must have at most two decimal places.");
        }

        if (creationDate > today)
        {
            throw new DomainException("investment.creation_in_future", "The creation date can be today or a past date, never a future date.");
        }

        if (creationDate < MinCreationDate)
        {
            throw new DomainException("investment.creation_too_old", $"The creation date can't be before {MinCreationDate:yyyy-MM-dd}.");
        }

        return new Investment
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            InitialAmount = amount,
            CreationDate = creationDate,
            Status = InvestmentStatus.Active,
        };
    }

    /// <summary>
    /// Balance on <paramref name="asOf"/>. For a withdrawn investment the balance is frozen
    /// at the withdrawal, whatever date is asked.
    /// </summary>
    public InvestmentBalance GetBalance(DateOnly asOf, InvestmentRules rules)
    {
        if (Withdrawal is not null)
        {
            return new InvestmentBalance(Withdrawal.Date, InitialAmount, Withdrawal.Gains, Withdrawal.GrossAmount);
        }

        var months = GainCalculator.CompletedMonths(CreationDate, asOf);
        var balance = GainCalculator.CalculateBalance(InitialAmount, rules.MonthlyGainRate, months);

        return new InvestmentBalance(asOf, InitialAmount, balance - InitialAmount, balance);
    }

    /// <summary>Simulates a full withdrawal on <paramref name="date"/> without changing the investment.</summary>
    public Withdrawal QuoteWithdrawal(DateOnly date, DateOnly today, InvestmentRules rules)
    {
        EnsureCanWithdraw(date, today);

        var balance = GetBalance(date, rules);
        var taxRate = TaxPolicy.GetRate(rules, CreationDate, date);
        var tax = Money.Round(balance.Gains * taxRate);

        return new Withdrawal(date, balance.CurrentBalance, balance.Gains, taxRate, tax, balance.CurrentBalance - tax);
    }

    /// <summary>Withdraws the whole balance (partial withdrawals are not supported).</summary>
    public Withdrawal Withdraw(DateOnly date, DateOnly today, InvestmentRules rules)
    {
        var withdrawal = QuoteWithdrawal(date, today, rules);

        Withdrawal = withdrawal;
        Status = InvestmentStatus.Withdrawn;

        return withdrawal;
    }

    private void EnsureCanWithdraw(DateOnly date, DateOnly today)
    {
        if (Status == InvestmentStatus.Withdrawn)
        {
            throw new DomainException("investment.already_withdrawn", "This investment has already been withdrawn.", DomainErrorKind.Conflict);
        }

        if (date < CreationDate)
        {
            throw new DomainException("withdrawal.before_creation", "The withdrawal date can't be before the investment creation date.");
        }

        if (date > today)
        {
            throw new DomainException("withdrawal.in_future", "The withdrawal date can't be in the future.");
        }
    }
}
