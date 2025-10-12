using System;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Xunit;

namespace HWInventory.Api.IntegrationTests;

public class WorkstationsCrudTests : IntegrationTestBase
{
    public WorkstationsCrudTests(ApiWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task WorkstationLifecycle_CompletesSuccessfully()
    {
        var operatingSystemId = await EnsureDictionaryEntryAsync("OperatingSystem", "WIN11", "Windows 11");
        var workstationTypeId = await EnsureDictionaryEntryAsync("WorkstationType", "LAPTOP", "Notebook");
        var locationId = await EnsureDictionaryEntryAsync("Location", "HQ-2F", "HQ 2nd Floor");
        var vlanId = await EnsureDictionaryEntryAsync("VLAN", "VLAN30", "Office");

        var createRequest = new WorkstationRequestDto(
            Name: "Integration Laptop",
            InventoryNumber: "WRK-001",
            OperatingSystemId: operatingSystemId,
            WorkstationTypeId: workstationTypeId,
            OwnerId: null,
            OwnerDisplayName: null,
            OwnerDepartment: null,
            LocationId: locationId,
            LocationNote: "Desk 12",
            Cpu: "Intel i7",
            Ram: "32 GB",
            Storage: "1 TB SSD",
            MacAddress: "00-11-22-33-44-55",
            PurchasedAt: new DateTime(2022, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            SupportUntil: new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            PrimaryAdministratorId: null,
            SecondaryAdministratorId: null,
            Notes: "Created from integration test",
            NetworkAssignments: new[]
            {
                new NetworkEndpointDto("LAN", vlanId, "10.20.0.15")
            });

        var createResponse = await Client.PostAsJsonAsync("/api/workstations", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<Workstation>();
        Assert.NotNull(created);
        Assert.Equal(EntityStatus.Active, created!.Status);

        var fetched = await Client.GetFromJsonAsync<Workstation>($"/api/workstations/{created.Id}");
        Assert.NotNull(fetched);
        Assert.Equal("WRK-001", fetched!.InventoryNumber);

        var updateRequest = createRequest with
        {
            Notes = "Updated from integration test",
            NetworkAssignments = new[]
            {
                new NetworkEndpointDto("LAN", vlanId, "10.20.0.25")
            }
        };

        var updateResponse = await Client.PutAsJsonAsync($"/api/workstations/{created.Id}", updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<Workstation>();
        Assert.NotNull(updated);
        Assert.Equal("10.20.0.25", updated!.NetworkAssignments.First().IpAddress);

        var retireResponse = await Client.PostAsync($"/api/workstations/{created.Id}/retire", null);
        retireResponse.EnsureSuccessStatusCode();
        var retired = await Client.GetFromJsonAsync<Workstation>($"/api/workstations/{created.Id}");
        Assert.NotNull(retired);
        Assert.Equal(EntityStatus.Retired, retired!.Status);

        var restoreResponse = await Client.PostAsync($"/api/workstations/{created.Id}/restore", null);
        restoreResponse.EnsureSuccessStatusCode();

        var restored = await Client.GetFromJsonAsync<Workstation>($"/api/workstations/{created.Id}");
        Assert.NotNull(restored);
        Assert.Equal(EntityStatus.Active, restored!.Status);

        var deleteResponse = await Client.DeleteAsync($"/api/workstations/{created.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var finalGet = await Client.GetAsync($"/api/workstations/{created.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, finalGet.StatusCode);
    }
}
