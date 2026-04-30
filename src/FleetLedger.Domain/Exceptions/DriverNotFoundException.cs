namespace FleetLedger.Domain.Exceptions;

public class DriverNotFoundException : Exception
{
    public string DriverId { get; }

    public DriverNotFoundException(string driverId)
        : base($"Driver '{driverId}' not found.")
    {
        DriverId = driverId;
    }
}