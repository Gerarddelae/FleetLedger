namespace FleetLedger.Domain.Exceptions;

public class DepotInactiveException : Exception
{
    public string DepotId { get; }

    public DepotInactiveException(string depotId)
        : base($"Depot '{depotId}' is inactive and cannot be assigned to a new driver.")
    {
        DepotId = depotId;
    }
}