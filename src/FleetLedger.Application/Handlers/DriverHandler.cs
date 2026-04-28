using FleetLedger.Application;
using FleetLedger.Domain;

namespace FleetLedger.Application.Handlers;

public class DriverHandler
{
    private readonly IDriverRepository _repository;
    private readonly IDepotRepository _depotRepository;

    public DriverHandler(IDriverRepository repository, IDepotRepository depotRepository)
    {
        _repository = repository;
        _depotRepository = depotRepository;
    }

    public async Task<Driver> Handle(CreateDriverCommand cmd, CancellationToken ct)
    {
        var exists = await _repository.ExistsWithLicenseNumberAsync(cmd.LicenseNumber, ct);
        if (exists)
            throw new InvalidOperationException($"Driver with license number '{cmd.LicenseNumber}' already exists.");

        if (!string.IsNullOrEmpty(cmd.DepotId))
        {
            var depot = await _depotRepository.GetByIdAsync(cmd.DepotId, ct)
                ?? throw new KeyNotFoundException($"Depot '{cmd.DepotId}' not found.");
            if (!depot.Active)
                throw new InvalidOperationException($"Depot '{cmd.DepotId}' is inactive and cannot be assigned.");
        }

        var driver = Driver.Create(
            cmd.FullName,
            cmd.LicenseNumber,
            cmd.LicenseCategory,
            cmd.LicenseExpires,
            cmd.Phone,
            cmd.DepotId
        );

        return await _repository.AddAsync(driver, ct);
    }

    public async Task<Driver> Handle(UpdateDriverCommand cmd, CancellationToken ct)
    {
        var driver = await _repository.GetByIdAsync(cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Driver '{cmd.Id}' not found.");

        var existingWithLicense = await _repository.FindByLicenseNumberAsync(cmd.LicenseNumber, ct);
        if (existingWithLicense != null && existingWithLicense.Id != cmd.Id)
            throw new InvalidOperationException($"Driver with license number '{cmd.LicenseNumber}' already exists.");

        if (!string.IsNullOrEmpty(cmd.DepotId))
        {
            var depot = await _depotRepository.GetByIdAsync(cmd.DepotId, ct)
                ?? throw new KeyNotFoundException($"Depot '{cmd.DepotId}' not found.");
            if (!depot.Active)
                throw new InvalidOperationException($"Depot '{cmd.DepotId}' is inactive and cannot be assigned.");
        }

        driver.Update(
            cmd.FullName,
            cmd.LicenseNumber,
            cmd.LicenseCategory,
            cmd.LicenseExpires,
            cmd.Phone,
            cmd.DepotId
        );

        await _repository.UpdateAsync(driver, ct);
        return driver;
    }

    public async Task Handle(DeactivateDriverCommand cmd, CancellationToken ct)
    {
        var driver = await _repository.GetByIdAsync(cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Driver '{cmd.Id}' not found.");

        driver.Deactivate();
        await _repository.UpdateAsync(driver, ct);
    }

    public async Task<List<Driver>> Handle(GetDriversQuery query, CancellationToken ct)
    {
        return await _repository.GetAllAsync(query.Active, query.DepotId, query.LicenseCategory, ct);
    }

    public async Task<Driver?> Handle(GetDriverByIdQuery query, CancellationToken ct)
    {
        return await _repository.GetByIdAsync(query.Id, ct);
    }
}
