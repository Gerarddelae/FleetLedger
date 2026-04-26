using FleetLedger.Application;
using FleetLedger.Application.Handlers;
using FleetLedger.Domain;
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
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Depot>> Create([FromBody] CreateDepotCommand cmd, CancellationToken ct)
    {
        try
        {
            var depot = await _handler.Handle(cmd, ct);
            return CreatedAtAction(nameof(GetById), new { id = depot.Id }, depot);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
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
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Depot>> GetById(string id, CancellationToken ct)
    {
        var depot = await _handler.Handle(new GetDepotByIdQuery(id), ct);
        if (depot == null)
            return NotFound(new { error = $"Depot '{id}' not found." });
        return Ok(depot);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Depot), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Depot>> Update(string id, [FromBody] UpdateDepotCommand cmd, CancellationToken ct)
    {
        try
        {
            var updatedCmd = new UpdateDepotCommand(id, cmd.Name, cmd.Address, cmd.City, cmd.Region, cmd.ManagerName, cmd.Phone);
            var depot = await _handler.Handle(updatedCmd, ct);
            return Ok(depot);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Depot '{id}' not found." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        try
        {
            await _handler.Handle(new DeactivateDepotCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Depot '{id}' not found." });
        }
    }
}