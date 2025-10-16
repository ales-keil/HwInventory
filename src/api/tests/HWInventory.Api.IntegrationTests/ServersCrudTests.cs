using System;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Xunit;

namespace HWInventory.Api.IntegrationTests;

public class ServersCrudTests : IntegrationTestBase
{
    public ServersCrudTests(ApiWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ServerLifecycle_CompletesSuccessfully()
    {
        var environmentId = await EnsureDictionaryEntryAsync("Environment", "PROD", "Production");
        var wsusPriorityId = await EnsureDictionaryEntryAsync("WsusPriority", "HIGH", "High");
        var operatingSystemId = await EnsureDictionaryEntryAsync("OperatingSystem", "WIN2022", "Windows Server 2022");
        var serverRoleId = await EnsureDictionaryEntryAsync("ServerRole", "APP", "Application");
        var locationId = await EnsureDictionaryEntryAsync("Location", "HQ-1F", "HQ 1st Floor");
        var vlanId = await EnsureDictionaryEntryAsync("VLAN", "VLAN10", "Production");

        var createRequest = new ServerRequestDto(
            Name: "Integration Server",
            InventoryNumber: "SRV-IT-001",
            Manufacturer: "Dell",
            Model: "PowerEdge",
            EnvironmentId: environmentId,
            WsusPriorityId: wsusPriorityId,
            OperatingSystemId: operatingSystemId,
            ServerRoleId: serverRoleId,
            PrimaryAdministratorId: null,
            SecondaryAdministratorId: null,
            LocationId: locationId,
            RackPosition: "R1-U5",
            PurchasedAt: new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SupportUntil: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Notes: "Created from integration test",
            NetworkAssignments: new[]
            {
                new NetworkEndpointDto("LAN", vlanId, "10.10.0.10")
            });

        var createResponse = await Client.PostAsJsonAsync("/api/servers", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<Server>();
        Assert.NotNull(created);
        Assert.Equal("Integration Server", created!.Name);
        Assert.Equal(EntityStatus.Active, created.Status);

        var fetched = await Client.GetFromJsonAsync<Server>($"/api/servers/{created.Id}");
        Assert.NotNull(fetched);
        Assert.Equal("SRV-IT-001", fetched!.InventoryNumber);

        var updateRequest = createRequest with
        {
            Name = "Integration Server Updated",
            Notes = "Updated from integration test",
            NetworkAssignments = new[]
            {
                new NetworkEndpointDto("DMZ", vlanId, "10.10.0.20")
            }
        };

        var updateResponse = await Client.PutAsJsonAsync($"/api/servers/{created.Id}", updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<Server>();
        Assert.NotNull(updated);
        Assert.Equal("Integration Server Updated", updated!.Name);
        Assert.Single(updated.NetworkAssignments);
        Assert.Equal("10.10.0.20", updated.NetworkAssignments.First().IpAddress);

        var retireResponse = await Client.PostAsync($"/api/servers/{created.Id}/retire", null);
        retireResponse.EnsureSuccessStatusCode();

        var retired = await Client.GetFromJsonAsync<Server>($"/api/servers/{created.Id}");
        Assert.NotNull(retired);
        Assert.Equal(EntityStatus.Retired, retired!.Status);

        var restoreResponse = await Client.PostAsync($"/api/servers/{created.Id}/restore", null);
        restoreResponse.EnsureSuccessStatusCode();

        var restored = await Client.GetFromJsonAsync<Server>($"/api/servers/{created.Id}");
        Assert.NotNull(restored);
        Assert.Equal(EntityStatus.Active, restored!.Status);

        var deleteResponse = await Client.DeleteAsync($"/api/servers/{created.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var finalGet = await Client.GetAsync($"/api/servers/{created.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, finalGet.StatusCode);
    }

    [Fact]
    public async Task ServerBatchActions_ApplyToSelectedItemsOnly()
    {
        var environmentId = await EnsureDictionaryEntryAsync("Environment", "QA", "QA");
        var wsusPriorityId = await EnsureDictionaryEntryAsync("WsusPriority", "MED", "Medium");
        var operatingSystemId = await EnsureDictionaryEntryAsync("OperatingSystem", "LINUX", "Ubuntu 22.04");
        var serverRoleId = await EnsureDictionaryEntryAsync("ServerRole", "DB", "Database");
        var locationId = await EnsureDictionaryEntryAsync("Location", "DC", "Datacenter");

        async Task<Guid> CreateServerAsync(string name)
        {
            var request = new ServerRequestDto(
                Name: name,
                InventoryNumber: $"INV-{name}",
                Manufacturer: "Generic",
                Model: "1U",
                EnvironmentId: environmentId,
                WsusPriorityId: wsusPriorityId,
                OperatingSystemId: operatingSystemId,
                ServerRoleId: serverRoleId,
                PrimaryAdministratorId: null,
                SecondaryAdministratorId: null,
                LocationId: locationId,
                RackPosition: null,
                PurchasedAt: null,
                SupportUntil: null,
                Notes: null,
                NetworkAssignments: Array.Empty<NetworkEndpointDto>());

            var response = await Client.PostAsJsonAsync("/api/servers", request);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<Server>();
            Assert.NotNull(created);
            return created!.Id;
        }

        var firstId = await CreateServerAsync("Batch-1");
        var secondId = await CreateServerAsync("Batch-2");
        var thirdId = await CreateServerAsync("Batch-3");

        var retirePayload = new BatchActionRequest { Ids = new[] { firstId, secondId } };
        var retireResponse = await Client.PostAsJsonAsync("/api/servers/batch/retire", retirePayload);
        retireResponse.EnsureSuccessStatusCode();

        var first = await Client.GetFromJsonAsync<Server>($"/api/servers/{firstId}");
        var second = await Client.GetFromJsonAsync<Server>($"/api/servers/{secondId}");
        var third = await Client.GetFromJsonAsync<Server>($"/api/servers/{thirdId}");

        Assert.Equal(EntityStatus.Retired, first!.Status);
        Assert.Equal(EntityStatus.Retired, second!.Status);
        Assert.Equal(EntityStatus.Active, third!.Status);

        var restorePayload = new BatchActionRequest { Ids = new[] { firstId, thirdId } };
        var restoreResponse = await Client.PostAsJsonAsync("/api/servers/batch/restore", restorePayload);
        restoreResponse.EnsureSuccessStatusCode();

        first = await Client.GetFromJsonAsync<Server>($"/api/servers/{firstId}");
        third = await Client.GetFromJsonAsync<Server>($"/api/servers/{thirdId}");

        Assert.Equal(EntityStatus.Active, first!.Status);
        Assert.Equal(EntityStatus.Active, third!.Status);
        Assert.Equal(EntityStatus.Retired, (await Client.GetFromJsonAsync<Server>($"/api/servers/{secondId}"))!.Status);

        var deletePayload = new BatchActionRequest { Ids = new[] { secondId } };
        var deleteResponse = await Client.PostAsJsonAsync("/api/servers/batch/delete", deletePayload);
        deleteResponse.EnsureSuccessStatusCode();

        var deletedCheck = await Client.GetAsync($"/api/servers/{secondId}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, deletedCheck.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await Client.GetAsync($"/api/servers/{firstId}")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await Client.GetAsync($"/api/servers/{thirdId}")).StatusCode);
    }

    [Fact]
    public async Task ServerFiltering_ReturnsExpectedSubset()
    {
        var environmentProd = await EnsureDictionaryEntryAsync("Environment", "PROD", "Production");
        var environmentTest = await EnsureDictionaryEntryAsync("Environment", "TEST", "Test");
        var wsusPriorityId = await EnsureDictionaryEntryAsync("WsusPriority", "STD", "Standard");
        var operatingSystemId = await EnsureDictionaryEntryAsync("OperatingSystem", "LINUX", "Linux");
        var serverRoleId = await EnsureDictionaryEntryAsync("ServerRole", "API", "API");
        var locationPrg = await EnsureDictionaryEntryAsync("Location", "PRG", "Praha");
        var locationBrn = await EnsureDictionaryEntryAsync("Location", "BRN", "Brno");

        async Task<Guid> CreateServerAsync(string name, string inventory, Guid environmentId, Guid locationId)
        {
            var request = new ServerRequestDto(
                Name: name,
                InventoryNumber: inventory,
                Manufacturer: "Generic",
                Model: "Virtual",
                EnvironmentId: environmentId,
                WsusPriorityId: wsusPriorityId,
                OperatingSystemId: operatingSystemId,
                ServerRoleId: serverRoleId,
                PrimaryAdministratorId: null,
                SecondaryAdministratorId: null,
                LocationId: locationId,
                RackPosition: null,
                PurchasedAt: null,
                SupportUntil: null,
                Notes: null,
                NetworkAssignments: Array.Empty<NetworkEndpointDto>());

            var response = await Client.PostAsJsonAsync("/api/servers", request);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<Server>();
            Assert.NotNull(created);
            return created!.Id;
        }

        var prodServerId = await CreateServerAsync("Alpha Prod", "SRV-PROD-01", environmentProd, locationPrg);
        var testServerId = await CreateServerAsync("Beta Test", "SRV-TEST-01", environmentTest, locationBrn);

        var retireResponse = await Client.PostAsync($"/api/servers/{testServerId}/retire", null);
        retireResponse.EnsureSuccessStatusCode();

        var searchResponse = await Client.GetFromJsonAsync<PagedResponse<Server>>($"/api/servers?page=1&size=50&search=Alpha");
        Assert.NotNull(searchResponse);
        Assert.Single(searchResponse!.Items);
        Assert.Equal(prodServerId, searchResponse.Items[0].Id);

        var statusResponse = await Client.GetFromJsonAsync<PagedResponse<Server>>($"/api/servers?page=1&size=50&status=Retired");
        Assert.NotNull(statusResponse);
        Assert.Single(statusResponse!.Items);
        Assert.Equal(testServerId, statusResponse.Items[0].Id);

        var locationResponse = await Client.GetFromJsonAsync<PagedResponse<Server>>($"/api/servers?page=1&size=50&locationId={locationPrg}");
        Assert.NotNull(locationResponse);
        Assert.Single(locationResponse!.Items);
        Assert.Equal(prodServerId, locationResponse.Items[0].Id);

        var environmentResponse = await Client.GetFromJsonAsync<PagedResponse<Server>>($"/api/servers?page=1&size=50&environmentId={environmentTest}");
        Assert.NotNull(environmentResponse);
        Assert.Single(environmentResponse!.Items);
        Assert.Equal(testServerId, environmentResponse.Items[0].Id);
    }

    private sealed record PagedResponse<T>(T[] Items, int TotalCount, int Page, int PageSize);
}
