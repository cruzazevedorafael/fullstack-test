using Investments.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Investments.Api.Errors;

/// <summary>Translates known exceptions into RFC 9457 problem details. Anything else becomes a generic 500.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, "Resource not found", e.Code),
            DomainException { Kind: DomainErrorKind.Conflict } e => (StatusCodes.Status409Conflict, "Conflict", e.Code),
            DomainException e => (StatusCodes.Status400BadRequest, "Business rule violation", e.Code),
            _ => (0, string.Empty, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        context.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message,
                Extensions = { ["code"] = code },
            },
        });
    }
}
