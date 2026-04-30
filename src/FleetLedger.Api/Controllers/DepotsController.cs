using FleetLedger.Application;
using FleetLedger.Application.Handlers;
using FleetLedger.Domain;
using FleetLedger.Api.Contracts.Requests;
using Microsoft.AspNetCore.Mvc;

namespace FleetLedger.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class DepotsController : ControllerBase
{
    private readonly DepotHandler _handler;

    public DepotsController(DepotHandler handler)
    {
        _handler = handler;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Depot), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Depot>> Create([FromBody] CreateDepotRequest req, CancellationToken ct)
    {
        var cmd = new CreateDepotCommand(req.Name, req.Address, req.City, req.Region, req.ManagerName, req.Phone);
        var depot = await _handler.Handle(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = depot.Id }, depot);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Depot>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Depot>>> GetAll([FromQuery] bool? active, [FromQuery] string? region, CancellationToken ct)
    {
        var depots = await _handler.Handle(new GetDepotsQuery(active, region), ct);
        return Ok(depots);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Depot), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Depot>> GetById(string id, CancellationToken ct)
    {
        var depot = await _handler.Handle(new GetDepotByIdQuery(id), ct);
        if (depot == null)
            return NotFound(new { error = $"Depot '{id}' not found." });
        return Ok(depot);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Depot), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Depot>> Update(string id, [FromBody] UpdateDepotRequest req, CancellationToken ct)
    {
        var cmd = new UpdateDepotCommand(id, req.Name, req.Address, req.City, req.Region, req.ManagerName, req.Phone);
        var depot = await _handler.Handle(cmd, ct);
        return Ok(depot);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _handler.Handle(new DeactivateDepotCommand(id), ct);
        return NoContent();
    }
}