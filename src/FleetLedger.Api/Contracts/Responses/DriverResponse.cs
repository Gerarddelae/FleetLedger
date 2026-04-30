namespace FleetLedger.Api.Contracts.Responses;

public class DriverResponse
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string LicenseCategory { get; set; } = string.Empty;
    public DateOnly LicenseExpires { get; set; }
    public string? Phone { get; set; }
    public string? DepotId { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
