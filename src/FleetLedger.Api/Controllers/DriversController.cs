using FleetLedger.Application;
using FleetLedger.Application.Handlers;
using FleetLedger.Api.Contracts.Requests;
using FleetLedger.Api.Contracts.Responses;
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
    [ProducesResponseType(typeof(DriverResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DriverResponse>> Create([FromBody] CreateDriverRequest req, CancellationToken ct)
    {
        var cmd = new CreateDriverCommand(req.FullName, req.LicenseNumber, req.LicenseCategory, req.LicenseExpires, req.Phone, req.DepotId);
        var driver = await _handler.Handle(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = driver.Id }, driver.ToResponse());
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<DriverResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DriverResponse>>> GetAll([FromQuery] bool? active, [FromQuery] string? depotId, [FromQuery] string? licenseCategory, CancellationToken ct)
    {
        var drivers = await _handler.Handle(new GetDriversQuery(active, depotId, licenseCategory), ct);
        return Ok(drivers.Select(d => d.ToResponse()).ToList());
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DriverResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverResponse>> GetById(string id, CancellationToken ct)
    {
        var driver = await _handler.Handle(new GetDriverByIdQuery(id), ct);
        if (driver == null)
            return NotFound(new { error = $"Driver '{id}' not found." });
        return Ok(driver.ToResponse());
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(DriverResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DriverResponse>> Update(string id, [FromBody] UpdateDriverRequest req, CancellationToken ct)
    {
        var cmd = new UpdateDriverCommand(id, req.FullName, req.LicenseNumber, req.LicenseCategory, req.LicenseExpires, req.Phone, req.DepotId);
        var driver = await _handler.Handle(cmd, ct);
        return Ok(driver.ToResponse());
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