namespace FleetLedger.Api.Contracts.Requests;

public record CreateDriverRequest(
    string FullName,
    string LicenseNumber,
    string LicenseCategory,
    DateOnly LicenseExpires,
    string? Phone = null,
    string? DepotId = null
);

public record UpdateDriverRequest(
    string FullName,
    string LicenseNumber,
    string LicenseCategory,
    DateOnly LicenseExpires,
    string? Phone = null,
    string? DepotId = null
);