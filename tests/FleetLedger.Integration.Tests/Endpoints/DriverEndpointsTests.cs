using System.Net;
using System.Net.Http.Json;
using FleetLedger.Api.Contracts.Requests;
using FleetLedger.Api.Contracts.Responses;
using FleetLedger.Integration.Tests.Fixtures;
using Xunit;

namespace FleetLedger.Integration.Tests.Endpoints;

[Collection("Integration Tests")]
public class DriverEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DriverEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateDriver_Returns201_AndValidId()
    {
        var request = new CreateDriverRequest("John Doe", "LICENSE123", "C", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));
        var response = await _client.PostAsJsonAsync("/drivers", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var driver = await response.Content.ReadFromJsonAsync<DriverResponse>();
        Assert.NotNull(driver);
        Assert.StartsWith("DRV-", driver.Id);
    }

    [Fact]
    public async Task CreateDriver_DuplicateLicense_Returns409()
    {
        var request = new CreateDriverRequest("John Doe", "DUPLICATE123", "C", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));

        await _client.PostAsJsonAsync("/drivers", request);
        var response = await _client.PostAsJsonAsync("/drivers", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateDriver_WithInactiveDepot_Returns422()
    {
        var depotRequest = new CreateDepotRequest("Test Inactive Depot", "123 Test St", "Test City");
        await _client.PostAsJsonAsync("/depots", depotRequest);

        var inactiveDepotResponse = await _client.GetAsync("/depots");
        var depots = await inactiveDepotResponse.Content.ReadFromJsonAsync<List<DepotResponse>>();
        var depot = depots!.First();
        await _client.DeleteAsync($"/depots/{depot.Id}");

        var driverRequest = new CreateDriverRequest("John Doe", "NEWLICENSE1", "C", DateOnly.FromDateTime(DateTime.Today.AddYears(1)), null, depot.Id);
        var response = await _client.PostAsJsonAsync("/drivers", driverRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetDriver_Existing_Returns200()
    {
        var createRequest = new CreateDriverRequest("Test Driver", "LICENSE456", "C", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));
        var createResponse = await _client.PostAsJsonAsync("/drivers", createRequest);
        var driver = await createResponse.Content.ReadFromJsonAsync<DriverResponse>();

        var response = await _client.GetAsync($"/drivers/{driver!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDriver_NonExisting_Returns404()
    {
        var response = await _client.GetAsync("/drivers/DRV-NONEXISTENT");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDriver_Existing_Returns200()
    {
        var createRequest = new CreateDriverRequest("Update Test", "UPDATELICENSE", "C", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));
        var createResponse = await _client.PostAsJsonAsync("/drivers", createRequest);
        var driver = await createResponse.Content.ReadFromJsonAsync<DriverResponse>();

        var updateRequest = new UpdateDriverRequest("Updated Name", "UPDATELICENSE", "D", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));
        var response = await _client.PutAsJsonAsync($"/drivers/{driver!.Id}", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<DriverResponse>();
        Assert.Equal("Updated Name", updated!.FullName);
    }

    [Fact]
    public async Task UpdateDriver_DuplicateLicense_Returns409()
    {
        var request1 = new CreateDriverRequest("First", "LIC1", "C", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));
        var request2 = new CreateDriverRequest("Second", "LIC2", "C", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));

        var r1 = await _client.PostAsJsonAsync("/drivers", request1);
        var d1 = await r1.Content.ReadFromJsonAsync<DriverResponse>();
        await _client.PostAsJsonAsync("/drivers", request2);

        var updateRequest = new UpdateDriverRequest("Second Name", "LIC2", "C", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));
        var response = await _client.PutAsJsonAsync($"/drivers/{d1!.Id}", updateRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDriver_Existing_Returns204_AndSetsInactive()
    {
        var createRequest = new CreateDriverRequest("Delete Test", "DELETELIC", "C", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));
        var createResponse = await _client.PostAsJsonAsync("/drivers", createRequest);
        var driver = await createResponse.Content.ReadFromJsonAsync<DriverResponse>();

        var response = await _client.DeleteAsync($"/drivers/{driver!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await _client.GetAsync("/drivers?active=true");
        var drivers = await getResponse.Content.ReadFromJsonAsync<List<DriverResponse>>();
        Assert.DoesNotContain(drivers!, d => d.Id == driver.Id);
    }

    [Fact]
    public async Task GetDrivers_FilterByDepot_ReturnsOnlyDriversOfThatDepot()
    {
        var depotRequest = new CreateDepotRequest("Filter Depot", "123 Test St", "Test City");
        var depotResponse = await _client.PostAsJsonAsync("/depots", depotRequest);
        var depot = await depotResponse.Content.ReadFromJsonAsync<DepotResponse>();

        var driverRequest1 = new CreateDriverRequest("Driver 1", "LICFILTER1", "C", DateOnly.FromDateTime(DateTime.Today.AddYears(1)), null, depot!.Id);
        var driverRequest2 = new CreateDriverRequest("Driver 2", "LICFILTER2", "C", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));

        await _client.PostAsJsonAsync("/drivers", driverRequest1);
        await _client.PostAsJsonAsync("/drivers", driverRequest2);

        var response = await _client.GetAsync($"/drivers?depotId={depot.Id}");
        var drivers = await response.Content.ReadFromJsonAsync<List<DriverResponse>>();

        var singleDriver = Assert.Single(drivers!);
        Assert.Equal(depot.Id, singleDriver.DepotId);
    }

    [Fact]
    public async Task GetDrivers_FilterByLicenseCategory_ReturnsFiltered()
    {
        var requestC = new CreateDriverRequest("Cat C", "CATCLIC", "C", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));
        var requestD = new CreateDriverRequest("Cat D", "CATDLIC", "D", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));

        await _client.PostAsJsonAsync("/drivers", requestC);
        await _client.PostAsJsonAsync("/drivers", requestD);

        var responseC = await _client.GetAsync("/drivers?licenseCategory=C");
        var cDrivers = await responseC.Content.ReadFromJsonAsync<List<DriverResponse>>();
        var responseD = await _client.GetAsync("/drivers?licenseCategory=D");
        var dDrivers = await responseD.Content.ReadFromJsonAsync<List<DriverResponse>>();

        Assert.Single(cDrivers!);
        Assert.Single(dDrivers!);
    }
}