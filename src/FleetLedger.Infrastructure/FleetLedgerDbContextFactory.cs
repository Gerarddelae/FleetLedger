using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FleetLedger.Infrastructure;

public class FleetLedgerDbContextFactory : IDesignTimeDbContextFactory<FleetLedgerDbContext>
{
    public FleetLedgerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FleetLedgerDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=fleetledger;Username=fleet;Password=fleet");
        return new FleetLedgerDbContext(optionsBuilder.Options);
    }
}