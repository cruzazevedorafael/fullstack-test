namespace Investments.Domain.Calculations;

/// <summary>
/// Numeric parameters of the investment product. Kept out of the code so they can be
/// changed through configuration (<c>appsettings.json</c>, section <c>InvestmentRules</c>).
/// </summary>
public sealed record InvestmentRules
{
    /// <summary>Gain paid on every monthly anniversary (0.52% = 0.0052).</summary>
    public decimal MonthlyGainRate { get; init; } = 0.0052m;

    /// <summary>Tax rate when the investment is less than one year old.</summary>
    public decimal TaxRateUnderOneYear { get; init; } = 0.225m;

    /// <summary>Tax rate when the investment is between one and two years old (both inclusive).</summary>
    public decimal TaxRateOneToTwoYears { get; init; } = 0.185m;

    /// <summary>Tax rate when the investment is older than two years.</summary>
    public decimal TaxRateOverTwoYears { get; init; } = 0.15m;

    public static InvestmentRules Default { get; } = new();
}
