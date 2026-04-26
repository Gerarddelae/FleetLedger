using FleetLedger.Domain;

namespace FleetLedger.Application;

public interface IDepotRepository
{
    Task<Depot?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<List<Depot>> GetAllAsync(bool? active = null, string? region = null, CancellationToken ct = default);
    Task<Depot> AddAsync(Depot depot, CancellationToken ct = default);
    Task UpdateAsync(Depot depot, CancellationToken ct = default);
    Task<bool> ExistsWithNameAsync(string name, CancellationToken ct = default);
    Task<Depot?> FindByNameAsync(string name, CancellationToken ct = default);
}