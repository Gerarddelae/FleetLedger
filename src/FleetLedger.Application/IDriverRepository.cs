using FleetLedger.Domain;

namespace FleetLedger.Application;

public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<List<Driver>> GetAllAsync(bool? active = null, string? depotId = null, string? licenseCategory = null, CancellationToken ct = default);
    Task<Driver> AddAsync(Driver driver, CancellationToken ct = default);
    Task UpdateAsync(Driver driver, CancellationToken ct = default);
    Task<bool> ExistsWithLicenseNumberAsync(string licenseNumber, CancellationToken ct = default);
    Task<Driver?> FindByLicenseNumberAsync(string licenseNumber, CancellationToken ct = default);
}
