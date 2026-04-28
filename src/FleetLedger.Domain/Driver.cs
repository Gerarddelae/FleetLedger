namespace FleetLedger.Domain;

public class Driver
{
    public string Id { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string LicenseNumber { get; private set; } = string.Empty;
    public string LicenseCategory { get; private set; } = string.Empty;
    public DateOnly LicenseExpires { get; private set; }
    public string? Phone { get; private set; }
    public string? DepotId { get; private set; }
    public bool Active { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Driver() { }

    public static Driver Create(string fullName, string licenseNumber, string licenseCategory, DateOnly licenseExpires, string? phone = null, string? depotId = null)
    {
        var now = DateTime.UtcNow;
        return new Driver
        {
            Id = GenerateId(now),
            FullName = fullName,
            LicenseNumber = licenseNumber,
            LicenseCategory = licenseCategory,
            LicenseExpires = licenseExpires,
            Phone = phone,
            DepotId = depotId,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static string GenerateId(DateTime timestamp)
    {
        return $"DRV-{timestamp:yyyyMMdd}-XXXX";
    }

    public void Update(string fullName, string licenseNumber, string licenseCategory, DateOnly licenseExpires, string? phone, string? depotId)
    {
        FullName = fullName;
        LicenseNumber = licenseNumber;
        LicenseCategory = licenseCategory;
        LicenseExpires = licenseExpires;
        Phone = phone;
        DepotId = depotId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        Active = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        Active = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
