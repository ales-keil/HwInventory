using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Xunit;

namespace HWInventory.Api.IntegrationTests;

public class NetworkDevicesCrudTests : IntegrationTestBase
{
    public NetworkDevicesCrudTests(ApiWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task NetworkDeviceLifecycle_CompletesSuccessfully()
    {
        var deviceTypeId = await EnsureDictionaryEntryAsync("DeviceType", "SWITCH", "Switch");
        var locationId = await EnsureDictionaryEntryAsync("Location", "HQ-1F", "HQ 1st Floor");
        var vlanId = await EnsureDictionaryEntryAsync("VLAN", "VLAN20", "DMZ");

        var createRequest = new NetworkDeviceRequestDto(
            Name: "Core Switch",
            InventoryNumber: "NET-001",
            DeviceTypeId: deviceTypeId,
            Manufacturer: "Cisco",
            Model: "Catalyst",
            LocationId: locationId,
            RackPosition: "R2-U10",
            PrimaryAdministratorId: null,
            SecondaryAdministratorId: null,
            SupportUntil: null,
            Notes: "Created from integration test",
            NetworkAssignments: new[]
            {
                new NetworkEndpointDto("LAN", vlanId, "192.168.0.1")
            });

        var createResponse = await Client.PostAsJsonAsync("/api/network-devices", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<NetworkDevice>();
        Assert.NotNull(created);
        Assert.Equal(EntityStatus.Active, created!.Status);

        var fetched = await Client.GetFromJsonAsync<NetworkDevice>($"/api/network-devices/{created.Id}");
        Assert.NotNull(fetched);
        Assert.Equal("NET-001", fetched!.InventoryNumber);

        var updateRequest = createRequest with
        {
            Notes = "Updated from integration test",
            NetworkAssignments = new[]
            {
                new NetworkEndpointDto("LAN", vlanId, "192.168.0.2")
            }
        };

        var updateResponse = await Client.PutAsJsonAsync($"/api/network-devices/{created.Id}", updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<NetworkDevice>();
        Assert.NotNull(updated);
        Assert.Equal("192.168.0.2", updated!.NetworkAssignments.First().IpAddress);

        var retireResponse = await Client.PostAsync($"/api/network-devices/{created.Id}/retire", null);
        retireResponse.EnsureSuccessStatusCode();
        var retired = await Client.GetFromJsonAsync<NetworkDevice>($"/api/network-devices/{created.Id}");
        Assert.NotNull(retired);
        Assert.Equal(EntityStatus.Retired, retired!.Status);

        var restoreResponse = await Client.PostAsync($"/api/network-devices/{created.Id}/restore", null);
        restoreResponse.EnsureSuccessStatusCode();

        var restored = await Client.GetFromJsonAsync<NetworkDevice>($"/api/network-devices/{created.Id}");
        Assert.NotNull(restored);
        Assert.Equal(EntityStatus.Active, restored!.Status);

        var deleteResponse = await Client.DeleteAsync($"/api/network-devices/{created.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var finalGet = await Client.GetAsync($"/api/network-devices/{created.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, finalGet.StatusCode);
    }
}
