using FleetLedger.Application;
using FleetLedger.Domain;
using FleetLedger.Domain.Exceptions;

namespace FleetLedger.Application.Handlers;

public class DepotHandler
{
    private readonly IDepotRepository _repository;

    public DepotHandler(IDepotRepository repository)
    {
        _repository = repository;
    }

    public async Task<Depot> Handle(CreateDepotCommand cmd, CancellationToken ct)
    {
        var exists = await _repository.ExistsWithNameAsync(cmd.Name, ct);
        if (exists)
            throw new DepotNameAlreadyExistsException(cmd.Name);

        var depot = Depot.Create(
            cmd.Name,
            cmd.Address,
            cmd.City,
            cmd.Region,
            cmd.ManagerName,
            cmd.Phone
        );

        return await _repository.AddAsync(depot, ct);
    }

    public async Task<Depot> Handle(UpdateDepotCommand cmd, CancellationToken ct)
    {
        var depot = await _repository.GetByIdAsync(cmd.Id, ct)
            ?? throw new DepotNotFoundException(cmd.Id);

        var existingWithName = await _repository.FindByNameAsync(cmd.Name, ct);
        if (existingWithName != null && existingWithName.Id != cmd.Id)
            throw new DepotNameAlreadyExistsException(cmd.Name);

        depot.Update(
            cmd.Name,
            cmd.Address,
            cmd.City,
            cmd.Region,
            cmd.ManagerName,
            cmd.Phone
        );

        await _repository.UpdateAsync(depot, ct);
        return depot;
    }

    public async Task Handle(DeactivateDepotCommand cmd, CancellationToken ct)
    {
        var depot = await _repository.GetByIdAsync(cmd.Id, ct)
            ?? throw new DepotNotFoundException(cmd.Id);

        depot.Deactivate();
        await _repository.UpdateAsync(depot, ct);
    }

    public async Task<List<Depot>> Handle(GetDepotsQuery query, CancellationToken ct)
    {
        return await _repository.GetAllAsync(query.Active, query.Region, ct);
    }

    public async Task<Depot?> Handle(GetDepotByIdQuery query, CancellationToken ct)
    {
        return await _repository.GetByIdAsync(query.Id, ct);
    }
}