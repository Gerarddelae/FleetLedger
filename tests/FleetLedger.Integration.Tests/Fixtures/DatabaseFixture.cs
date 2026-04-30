using FleetLedger.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace FleetLedger.Integration.Tests.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("fleetledger")
        .WithUsername("fleet")
        .WithPassword("fleet")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly DatabaseFixture _dbFixture;

    public TestWebApplicationFactory(DatabaseFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public string ConnectionString => _dbFixture.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _dbFixture.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        await _dbFixture.InitializeAsync();
        
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetLedgerDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbFixture.DisposeAsync();
    }
}

[CollectionDefinition("Integration Tests")]
public class IntegrationTestsCollection : ICollectionFixture<DatabaseFixture>
{
}