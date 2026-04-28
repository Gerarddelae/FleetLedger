using FleetLedger.Application;
using FleetLedger.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetLedger.Infrastructure;

public class DriverRepository : IDriverRepository
{
    private readonly FleetLedgerDbContext _context;

    public DriverRepository(FleetLedgerDbContext context)
    {
        _context = context;
    }

    public async Task<Driver?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _context.Drivers.FindAsync([id], ct);
    }

    public async Task<List<Driver>> GetAllAsync(bool? active = null, string? depotId = null, string? licenseCategory = null, CancellationToken ct = default)
    {
        var query = _context.Drivers.AsQueryable();

        if (active.HasValue)
            query = query.Where(d => d.Active == active.Value);

        if (!string.IsNullOrEmpty(depotId))
            query = query.Where(d => d.DepotId == depotId);

        if (!string.IsNullOrEmpty(licenseCategory))
            query = query.Where(d => d.LicenseCategory == licenseCategory);

        return await query.ToListAsync(ct);
    }

    public async Task<Driver> AddAsync(Driver driver, CancellationToken ct = default)
    {
        _context.Drivers.Add(driver);
        await _context.SaveChangesAsync(ct);
        return driver;
    }

    public async Task UpdateAsync(Driver driver, CancellationToken ct = default)
    {
        _context.Drivers.Update(driver);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsWithLicenseNumberAsync(string licenseNumber, CancellationToken ct = default)
    {
        return await _context.Drivers.AnyAsync(d => d.LicenseNumber == licenseNumber, ct);
    }

    public async Task<Driver?> FindByLicenseNumberAsync(string licenseNumber, CancellationToken ct = default)
    {
        return await _context.Drivers.FirstOrDefaultAsync(d => d.LicenseNumber == licenseNumber, ct);
    }
}
