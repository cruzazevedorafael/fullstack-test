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

/// <summary>What an active investment has earned so far and what comes next.</summary>
public sealed record InvestmentAccrual(
    decimal MonthlyGainRate,
    int CompletedMonths,
    DateOnly NextGainDate,
    decimal NextGainAmount,
    Withdrawal WithdrawalToday);

/// <summary>Balance right after a monthly payment, already made or <paramref name="Projected"/>.</summary>
public sealed record GrowthPoint(DateOnly Date, decimal Balance, bool Projected);

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

    /// <summary>Longest term (50 years) accepted by <see cref="Simulate"/>.</summary>
    public const int MaxSimulationMonths = 600;

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

        EnsureValidTerms(amount, creationDate, today);

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
    /// Values of a withdrawal <paramref name="months"/> after the creation. Nothing is created, so unlike
    /// a real withdrawal it can be in the future.
    /// </summary>
    public static Withdrawal Simulate(decimal amount, DateOnly creationDate, int months, DateOnly today, InvestmentRules rules)
    {
        EnsureValidTerms(amount, creationDate, today);

        if (months is < 1 or > MaxSimulationMonths)
        {
            throw new DomainException("simulation.months_out_of_range", $"The term must be between 1 and {MaxSimulationMonths} months.");
        }

        return CalculateWithdrawal(amount, creationDate, creationDate.AddMonths(months), rules);
    }

    private static void EnsureValidTerms(decimal amount, DateOnly creationDate, DateOnly today)
    {
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

    /// <summary>Null once withdrawn: nothing else is paid.</summary>
    public InvestmentAccrual? GetAccrual(DateOnly today, InvestmentRules rules)
    {
        if (Status == InvestmentStatus.Withdrawn)
        {
            return null;
        }

        var months = GainCalculator.CompletedMonths(CreationDate, today);
        var current = PointAt(months, rules, projected: false);
        var next = PointAt(months + 1, rules, projected: true);

        return new InvestmentAccrual(
            rules.MonthlyGainRate, months, next.Date, next.Balance - current.Balance, QuoteWithdrawal(today, today, rules));
    }

    /// <summary>
    /// Balance after each monthly payment, from the creation to <paramref name="today"/> (or to the withdrawal),
    /// followed by <paramref name="projectedMonths"/> future payments while the investment is active.
    /// </summary>
    public IReadOnlyList<GrowthPoint> GetGrowth(DateOnly today, int projectedMonths, InvestmentRules rules)
    {
        var paid = GainCalculator.CompletedMonths(CreationDate, Withdrawal?.Date ?? today);
        var total = Status == InvestmentStatus.Withdrawn ? paid : paid + projectedMonths;

        return [.. Enumerable.Range(0, total + 1).Select(month => PointAt(month, rules, projected: month > paid))];
    }

    /// <summary>Simulates a full withdrawal on <paramref name="date"/> without changing the investment.</summary>
    public Withdrawal QuoteWithdrawal(DateOnly date, DateOnly today, InvestmentRules rules)
    {
        EnsureCanWithdraw(date, today);

        return CalculateWithdrawal(InitialAmount, CreationDate, date, rules);
    }

    /// <summary>Withdraws the whole balance (partial withdrawals are not supported).</summary>
    public Withdrawal Withdraw(DateOnly date, DateOnly today, InvestmentRules rules)
    {
        var withdrawal = QuoteWithdrawal(date, today, rules);

        Withdrawal = withdrawal;
        Status = InvestmentStatus.Withdrawn;

        return withdrawal;
    }

    private static Withdrawal CalculateWithdrawal(decimal amount, DateOnly creationDate, DateOnly date, InvestmentRules rules)
    {
        var months = GainCalculator.CompletedMonths(creationDate, date);
        var balance = GainCalculator.CalculateBalance(amount, rules.MonthlyGainRate, months);
        var gains = balance - amount;
        var taxRate = TaxPolicy.GetRate(rules, creationDate, date);
        var tax = Money.Round(gains * taxRate);

        return new Withdrawal(date, balance, gains, taxRate, tax, balance - tax);
    }

    private GrowthPoint PointAt(int month, InvestmentRules rules, bool projected) => new(
        CreationDate.AddMonths(month),
        GainCalculator.CalculateBalance(InitialAmount, rules.MonthlyGainRate, month),
        projected);

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
