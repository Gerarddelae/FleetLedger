using FleetLedger.Application;
using FleetLedger.Application.Handlers;
using FleetLedger.Domain;
using Microsoft.AspNetCore.Mvc;

namespace FleetLedger.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class DriversController : ControllerBase
{
    private readonly DriverHandler _handler;

    public DriversController(DriverHandler handler)
    {
        _handler = handler;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Driver), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Driver>> Create([FromBody] CreateDriverCommand cmd, CancellationToken ct)
    {
        try
        {
            var driver = await _handler.Handle(cmd, ct);
            return CreatedAtAction(nameof(GetById), new { id = driver.Id }, driver);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("inactive"))
                return UnprocessableEntity(new { error = ex.Message });
            return Conflict(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Driver>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Driver>>> GetAll([FromQuery] bool? active, [FromQuery] string? depotId, [FromQuery] string? licenseCategory, CancellationToken ct)
    {
        var drivers = await _handler.Handle(new GetDriversQuery(active, depotId, licenseCategory), ct);
        return Ok(drivers);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Driver), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Driver>> GetById(string id, CancellationToken ct)
    {
        var driver = await _handler.Handle(new GetDriverByIdQuery(id), ct);
        if (driver == null)
            return NotFound(new { error = $"Driver '{id}' not found." });
        return Ok(driver);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Driver), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Driver>> Update(string id, [FromBody] UpdateDriverCommand cmd, CancellationToken ct)
    {
        try
        {
            var updatedCmd = new UpdateDriverCommand(id, cmd.FullName, cmd.LicenseNumber, cmd.LicenseCategory, cmd.LicenseExpires, cmd.Phone, cmd.DepotId);
            var driver = await _handler.Handle(updatedCmd, ct);
            return Ok(driver);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("inactive"))
                return UnprocessableEntity(new { error = ex.Message });
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
            await _handler.Handle(new DeactivateDriverCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Driver '{id}' not found." });
        }
    }
}
