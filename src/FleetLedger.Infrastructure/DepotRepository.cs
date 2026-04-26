using FleetLedger.Application;
using FleetLedger.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetLedger.Infrastructure;

public class DepotRepository : IDepotRepository
{
    private readonly FleetLedgerDbContext _context;

    public DepotRepository(FleetLedgerDbContext context)
    {
        _context = context;
    }

    public async Task<Depot?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _context.Depots.FindAsync([id], ct);
    }

    public async Task<List<Depot>> GetAllAsync(bool? active = null, string? region = null, CancellationToken ct = default)
    {
        var query = _context.Depots.AsQueryable();

        if (active.HasValue)
            query = query.Where(d => d.Active == active.Value);

        if (!string.IsNullOrEmpty(region))
            query = query.Where(d => d.Region == region);

        return await query.ToListAsync(ct);
    }

    public async Task<Depot> AddAsync(Depot depot, CancellationToken ct = default)
    {
        _context.Depots.Add(depot);
        await _context.SaveChangesAsync(ct);
        return depot;
    }

    public async Task UpdateAsync(Depot depot, CancellationToken ct = default)
    {
        _context.Depots.Update(depot);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsWithNameAsync(string name, CancellationToken ct = default)
    {
        return await _context.Depots.AnyAsync(d => d.Name == name, ct);
    }

    public async Task<Depot?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        return await _context.Depots.FirstOrDefaultAsync(d => d.Name == name, ct);
    }
}