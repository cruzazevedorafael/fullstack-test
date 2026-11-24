namespace Investments.Domain.Exceptions;

/// <summary>Classifies a business rule violation so the API can map it to the right HTTP status.</summary>
public enum DomainErrorKind
{
    /// <summary>The input breaks a rule (e.g. negative amount, future date).</summary>
    Validation,

    /// <summary>The operation conflicts with the current state (e.g. withdrawing twice).</summary>
    Conflict,
}

/// <summary>Raised when a business rule is violated.</summary>
public sealed class DomainException(string code, string message, DomainErrorKind kind = DomainErrorKind.Validation)
    : Exception(message)
{
    /// <summary>Stable machine-readable identifier, e.g. <c>investment.amount_not_positive</c>.</summary>
    public string Code { get; } = code;

    public DomainErrorKind Kind { get; } = kind;
}
