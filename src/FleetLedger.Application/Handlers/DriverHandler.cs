using FleetLedger.Application;
using FleetLedger.Domain;
using FleetLedger.Domain.Exceptions;

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
            throw new LicenseNumberAlreadyExistsException(cmd.LicenseNumber);

        if (!string.IsNullOrEmpty(cmd.DepotId))
        {
            var depot = await _depotRepository.GetByIdAsync(cmd.DepotId, ct)
                ?? throw new DepotNotFoundException(cmd.DepotId);
            if (!depot.Active)
                throw new DepotInactiveException(cmd.DepotId);
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
            ?? throw new DriverNotFoundException(cmd.Id);

        var existingWithLicense = await _repository.FindByLicenseNumberAsync(cmd.LicenseNumber, ct);
        if (existingWithLicense != null && existingWithLicense.Id != cmd.Id)
            throw new LicenseNumberAlreadyExistsException(cmd.LicenseNumber);

        if (!string.IsNullOrEmpty(cmd.DepotId))
        {
            var depot = await _depotRepository.GetByIdAsync(cmd.DepotId, ct)
                ?? throw new DepotNotFoundException(cmd.DepotId);
            if (!depot.Active)
                throw new DepotInactiveException(cmd.DepotId);
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
            ?? throw new DriverNotFoundException(cmd.Id);

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
