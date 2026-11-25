namespace Investments.Domain.Calculations;

/// <summary>Regressive tax table: the older the investment, the lower the rate.</summary>
public static class TaxPolicy
{
    /// <summary>
    /// Rate applied to the gains, based on the investment age on the withdrawal date:
    /// <list type="bullet">
    /// <item>less than one year: <see cref="InvestmentRules.TaxRateUnderOneYear"/>;</item>
    /// <item>from exactly one year up to exactly two years: <see cref="InvestmentRules.TaxRateOneToTwoYears"/>;</item>
    /// <item>more than two years: <see cref="InvestmentRules.TaxRateOverTwoYears"/>.</item>
    /// </list>
    /// </summary>
    public static decimal GetRate(InvestmentRules rules, DateOnly creationDate, DateOnly withdrawalDate)
    {
        if (withdrawalDate < creationDate.AddYears(1))
        {
            return rules.TaxRateUnderOneYear;
        }

        if (withdrawalDate <= creationDate.AddYears(2))
        {
            return rules.TaxRateOneToTwoYears;
        }

        return rules.TaxRateOverTwoYears;
    }
}
