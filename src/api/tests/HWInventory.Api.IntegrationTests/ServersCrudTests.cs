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
}
