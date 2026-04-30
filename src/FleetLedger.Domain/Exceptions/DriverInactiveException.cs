namespace FleetLedger.Domain.Exceptions;

public class DriverInactiveException : Exception
{
    public string DriverId { get; }

    public DriverInactiveException(string driverId)
        : base($"Driver '{driverId}' is inactive and cannot be assigned.")
    {
        DriverId = driverId;
    }
}