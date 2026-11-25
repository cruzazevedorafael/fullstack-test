using System.ComponentModel.DataAnnotations;
using Investments.Api.Contracts;
using Investments.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Investments.Api.Controllers;

[ApiController]
[Route("api/investments")]
[Produces("application/json")]
[Tags("Investments")]
public sealed class InvestmentsController(InvestmentService investments) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List investments")]
    [EndpointDescription("Paginated, newest first. Use ownerId to list the investments of a single person.")]
    [ProducesResponseType<PagedResponse<InvestmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResponse<InvestmentResponse>> List([FromQuery] ListInvestmentsRequest request, CancellationToken cancellationToken) =>
        investments.ListAsync(request, cancellationToken);

    [HttpGet("{id:guid}")]
    [EndpointSummary("View an investment")]
    [EndpointDescription("Initial amount, gains and expected balance. Frozen at the withdrawal date once withdrawn.")]
    [ProducesResponseType<InvestmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<InvestmentResponse> Get(Guid id, CancellationToken cancellationToken) =>
        investments.GetAsync(id, cancellationToken);

    [HttpPost]
    [EndpointSummary("Create an investment")]
    [ProducesResponseType<InvestmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<InvestmentResponse>> Create(CreateInvestmentRequest request, CancellationToken cancellationToken)
    {
        var investment = await investments.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = investment.Id }, investment);
    }

    [HttpGet("{id:guid}/withdrawal-preview")]
    [EndpointSummary("Simulate a withdrawal")]
    [EndpointDescription("Returns gains, taxes and net amount for the given date without changing anything.")]
    [ProducesResponseType<WithdrawalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<WithdrawalResponse> PreviewWithdrawal(Guid id, [FromQuery, Required] DateOnly? date, CancellationToken cancellationToken) =>
        investments.QuoteWithdrawalAsync(id, date!.Value, cancellationToken);

    [HttpPost("{id:guid}/withdrawal")]
    [EndpointSummary("Withdraw an investment")]
    [EndpointDescription("Withdraws the whole balance. Taxes apply only to the gains. Can't be repeated or undone.")]
    [ProducesResponseType<InvestmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<InvestmentResponse> Withdraw(Guid id, WithdrawRequest request, CancellationToken cancellationToken) =>
        investments.WithdrawAsync(id, request.Date!.Value, cancellationToken);
}
