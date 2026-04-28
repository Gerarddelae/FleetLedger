namespace FleetLedger.Application;

public record CreateDriverCommand(
    string FullName,
    string LicenseNumber,
    string LicenseCategory,
    DateOnly LicenseExpires,
    string? Phone = null,
    string? DepotId = null
);

public record UpdateDriverCommand(
    string Id,
    string FullName,
    string LicenseNumber,
    string LicenseCategory,
    DateOnly LicenseExpires,
    string? Phone,
    string? DepotId
);

public record DeactivateDriverCommand(string Id);

public record GetDriversQuery(bool? Active = null, string? DepotId = null, string? LicenseCategory = null);

public record GetDriverByIdQuery(string Id);
