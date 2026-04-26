namespace FleetLedger.Domain;

public class Depot
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string? Region { get; private set; }
    public string? ManagerName { get; private set; }
    public string? Phone { get; private set; }
    public bool Active { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Depot() { }

    public static Depot Create(string name, string address, string city, string? region = null, string? managerName = null, string? phone = null)
    {
        var now = DateTime.UtcNow;
        return new Depot
        {
            Id = GenerateId(now),
            Name = name,
            Address = address,
            City = city,
            Region = region,
            ManagerName = managerName,
            Phone = phone,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static string GenerateId(DateTime timestamp)
    {
        return $"DEP-{timestamp:yyyyMMdd}-XXXX";
    }

    public void Update(string name, string address, string city, string? region, string? managerName, string? phone)
    {
        Name = name;
        Address = address;
        City = city;
        Region = region;
        ManagerName = managerName;
        Phone = phone;
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