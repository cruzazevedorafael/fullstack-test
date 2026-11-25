using Investments.Api.Contracts;
using Investments.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Investments.Api.Controllers;

[ApiController]
[Route("api/owners")]
[Produces("application/json")]
[Tags("Owners")]
public sealed class OwnersController(OwnerService owners) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List people")]
    public Task<IReadOnlyList<OwnerResponse>> List(CancellationToken cancellationToken) =>
        owners.ListAsync(cancellationToken);

    [HttpPost]
    [EndpointSummary("Register a person")]
    [ProducesResponseType<OwnerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OwnerResponse>> Create(CreateOwnerRequest request, CancellationToken cancellationToken)
    {
        var owner = await owners.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, owner);
    }
}
