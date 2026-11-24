namespace Investments.Domain.Calculations;

/// <summary>Monetary rounding convention used across the domain.</summary>
public static class Money
{
    /// <summary>Rounds to cents, half away from zero (the usual commercial rounding).</summary>
    public static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static bool HasAtMostTwoDecimals(decimal value) => Round(value) == value;
}
