namespace FleetLedger.Application;

public record CreateDepotCommand(
    string Name,
    string Address,
    string City,
    string? Region = null,
    string? ManagerName = null,
    string? Phone = null
);

public record UpdateDepotCommand(
    string Id,
    string Name,
    string Address,
    string City,
    string? Region,
    string? ManagerName,
    string? Phone
);

public record DeactivateDepotCommand(string Id);

public record GetDepotsQuery(bool? Active = null, string? Region = null);

public record GetDepotByIdQuery(string Id);