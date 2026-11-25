namespace Investments.Api.Errors;

public sealed class NotFoundException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
