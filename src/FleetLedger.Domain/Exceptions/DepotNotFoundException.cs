namespace FleetLedger.Domain.Exceptions;

public class DepotNotFoundException : Exception
{
    public string DepotId { get; }

    public DepotNotFoundException(string depotId)
        : base($"Depot '{depotId}' not found.")
    {
        DepotId = depotId;
    }
}