using System.Net;
using System.Net.Http.Json;
using FleetLedger.Api.Contracts.Requests;
using FleetLedger.Domain;
using FleetLedger.Integration.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FleetLedger.Integration.Tests.Endpoints;

[Collection("Integration Tests")]
public class DepotEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DepotEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateDepot_Returns201_AndValidId()
    {
        var request = new CreateDepotRequest("Test Depot", "123 Test St", "Test City", "Test Region");
        var response = await _client.PostAsJsonAsync("/depots", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var depot = await response.Content.ReadFromJsonAsync<Depot>();
        Assert.NotNull(depot);
        Assert.StartsWith("DEP-", depot.Id);
    }

    [Fact]
    public async Task CreateDepot_DuplicateName_Returns409()
    {
        var request = new CreateDepotRequest("Duplicate Depot", "123 Test St", "Test City");

        await _client.PostAsJsonAsync("/depots", request);
        var response = await _client.PostAsJsonAsync("/depots", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetDepot_Existing_Returns200()
    {
        var createRequest = new CreateDepotRequest("Get Test Depot", "123 Test St", "Test City");
        var createResponse = await _client.PostAsJsonAsync("/depots", createRequest);
        var depot = await createResponse.Content.ReadFromJsonAsync<Depot>();

        var response = await _client.GetAsync($"/depots/{depot!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDepot_NonExisting_Returns404()
    {
        var response = await _client.GetAsync("/depots/DEP-NONEXISTENT");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDepot_Existing_Returns200()
    {
        var createRequest = new CreateDepotRequest("Update Test Depot", "123 Test St", "Test City");
        var createResponse = await _client.PostAsJsonAsync("/depots", createRequest);
        var depot = await createResponse.Content.ReadFromJsonAsync<Depot>();

        var updateRequest = new UpdateDepotRequest("Updated Depot", "456 New St", "New City");
        var response = await _client.PutAsJsonAsync($"/depots/{depot!.Id}", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Depot>();
        Assert.Equal("Updated Depot", updated!.Name);
    }

    [Fact]
    public async Task UpdateDepot_DuplicateName_Returns409()
    {
        var request1 = new CreateDepotRequest("First Depot", "123 Test St", "Test City");
        var request2 = new CreateDepotRequest("Second Depot", "456 Test St", "Test City");

        var r1 = await _client.PostAsJsonAsync("/depots", request1);
        var d1 = await r1.Content.ReadFromJsonAsync<Depot>();
        await _client.PostAsJsonAsync("/depots", request2);

        var updateRequest = new UpdateDepotRequest("First Depot", "123 Test St", "Test City");
        var response = await _client.PutAsJsonAsync($"/depots/{d1!.Id}", updateRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDepot_Existing_Returns204_AndSetsInactive()
    {
        var createRequest = new CreateDepotRequest("Delete Test Depot", "123 Test St", "Test City");
        var createResponse = await _client.PostAsJsonAsync("/depots", createRequest);
        var depot = await createResponse.Content.ReadFromJsonAsync<Depot>();

        var response = await _client.DeleteAsync($"/depots/{depot!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await _client.GetAsync("/depots?active=true");
        var depots = await getResponse.Content.ReadFromJsonAsync<List<Depot>>();
        Assert.DoesNotContain(depots!, d => d.Id == depot.Id);
    }

    [Fact]
    public async Task GetDepots_FilterByActive_ReturnsFiltered()
    {
        var request = new CreateDepotRequest("Active Depot", "123 Test St", "Test City");
        await _client.PostAsJsonAsync("/depots", request);

        await _client.PostAsJsonAsync("/depots", new CreateDepotRequest("Inactive Depot", "123 Test St", "Test City"));

        var activeResponse = await _client.GetAsync("/depots?active=true");
        var activeDepots = await activeResponse.Content.ReadFromJsonAsync<List<Depot>>();

        activeResponse = await _client.GetAsync("/depots?active=false");
        var inactiveDepots = await activeResponse.Content.ReadFromJsonAsync<List<Depot>>();

        Assert.NotEmpty(activeDepots!);
        Assert.NotEmpty(inactiveDepots!);
    }
}