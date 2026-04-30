using FleetLedger.Application;
using FleetLedger.Application.Handlers;
using FleetLedger.Domain;
using FleetLedger.Api.Contracts.Requests;
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Driver>> Create([FromBody] CreateDriverRequest req, CancellationToken ct)
    {
        var cmd = new CreateDriverCommand(req.FullName, req.LicenseNumber, req.LicenseCategory, req.LicenseExpires, req.Phone, req.DepotId);
        var driver = await _handler.Handle(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = driver.Id }, driver);
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Driver>> GetById(string id, CancellationToken ct)
    {
        var driver = await _handler.Handle(new GetDriverByIdQuery(id), ct);
        if (driver == null)
            return NotFound(new { error = $"Driver '{id}' not found." });
        return Ok(driver);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Driver), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Driver>> Update(string id, [FromBody] UpdateDriverRequest req, CancellationToken ct)
    {
        var cmd = new UpdateDriverCommand(id, req.FullName, req.LicenseNumber, req.LicenseCategory, req.LicenseExpires, req.Phone, req.DepotId);
        var driver = await _handler.Handle(cmd, ct);
        return Ok(driver);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _handler.Handle(new DeactivateDriverCommand(id), ct);
        return NoContent();
    }
}