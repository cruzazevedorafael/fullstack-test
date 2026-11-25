using Investments.Domain.Calculations;
using Microsoft.AspNetCore.Mvc;

namespace Investments.Api.Controllers;

[ApiController]
[Route("api/rules")]
[Produces("application/json")]
[Tags("Rules")]
public sealed class RulesController(InvestmentRules rules) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("View the rates")]
    [EndpointDescription("Monthly gain and tax rates currently in use, as fractions (0.0052 is 0.52%).")]
    [ProducesResponseType<InvestmentRules>(StatusCodes.Status200OK)]
    public InvestmentRules Get() => rules;
}
