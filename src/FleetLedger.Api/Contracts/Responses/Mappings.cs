using FleetLedger.Domain;

namespace FleetLedger.Api.Contracts.Responses;

internal static class Mappings
{
    public static DepotResponse ToResponse(this Depot depot) => new()
    {
        Id = depot.Id,
        Name = depot.Name,
        Address = depot.Address,
        City = depot.City,
        Region = depot.Region,
        ManagerName = depot.ManagerName,
        Phone = depot.Phone,
        Active = depot.Active,
        CreatedAt = depot.CreatedAt,
        UpdatedAt = depot.UpdatedAt,
    };

    public static DriverResponse ToResponse(this Driver driver) => new()
    {
        Id = driver.Id,
        FullName = driver.FullName,
        LicenseNumber = driver.LicenseNumber,
        LicenseCategory = driver.LicenseCategory,
        LicenseExpires = driver.LicenseExpires,
        Phone = driver.Phone,
        DepotId = driver.DepotId,
        Active = driver.Active,
        CreatedAt = driver.CreatedAt,
        UpdatedAt = driver.UpdatedAt,
    };
}
