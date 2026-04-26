using FleetLedger.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetLedger.Infrastructure;

public class FleetLedgerDbContext : DbContext
{
    public FleetLedgerDbContext(DbContextOptions<FleetLedgerDbContext> options) : base(options) { }

    public DbSet<Depot> Depots => Set<Depot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DepotConfiguration());
    }
}

public class DepotConfiguration : IEntityTypeConfiguration<Depot>
{
    public void Configure(EntityTypeBuilder<Depot> builder)
    {
        builder.ToTable("depots");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Address)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.City)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Region)
            .HasMaxLength(200);

        builder.Property(d => d.ManagerName)
            .HasMaxLength(200);

        builder.Property(d => d.Phone)
            .HasMaxLength(50);

        builder.HasIndex(d => d.Name)
            .IsUnique();

        builder.Property(d => d.Active)
            .HasDefaultValue(true);

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .IsRequired();
    }
}