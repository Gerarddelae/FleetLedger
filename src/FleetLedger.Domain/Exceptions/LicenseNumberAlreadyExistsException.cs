namespace FleetLedger.Domain.Exceptions;

public class LicenseNumberAlreadyExistsException : Exception
{
    public string LicenseNumber { get; }

    public LicenseNumberAlreadyExistsException(string licenseNumber)
        : base($"A driver with license number '{licenseNumber}' already exists.")
    {
        LicenseNumber = licenseNumber;
    }
}