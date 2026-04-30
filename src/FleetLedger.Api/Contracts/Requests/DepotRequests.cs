namespace FleetLedger.Api.Contracts.Requests;

public record CreateDepotRequest(
    string Name,
    string Address,
    string City,
    string? Region = null,
    string? ManagerName = null,
    string? Phone = null
);

public record UpdateDepotRequest(
    string Name,
    string Address,
    string City,
    string? Region = null,
    string? ManagerName = null,
    string? Phone = null
);