namespace FleetLedger.Domain.Exceptions;

public class DepotNameAlreadyExistsException : Exception
{
    public string Name { get; }

    public DepotNameAlreadyExistsException(string name)
        : base($"A depot with name '{name}' already exists.")
    {
        Name = name;
    }
}