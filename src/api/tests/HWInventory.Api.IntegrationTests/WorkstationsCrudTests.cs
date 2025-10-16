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

    [Fact]
    public async Task WorkstationFiltering_ReturnsExpectedSubset()
    {
        var osWindows = await EnsureDictionaryEntryAsync("OperatingSystem", "WIN10", "Windows 10");
        var osLinux = await EnsureDictionaryEntryAsync("OperatingSystem", "LINUX", "Linux");
        var typeLaptop = await EnsureDictionaryEntryAsync("WorkstationType", "LAPTOP", "Notebook");
        var typeThin = await EnsureDictionaryEntryAsync("WorkstationType", "THIN", "Thin Client");
        var locationHq = await EnsureDictionaryEntryAsync("Location", "HQ", "Headquarters");
        var locationRemote = await EnsureDictionaryEntryAsync("Location", "REMOTE", "Remote Office");

        async Task<Guid> CreateWorkstationAsync(string name, string inventory, Guid osId, Guid typeId, Guid locationId)
        {
            var request = new WorkstationRequestDto(
                Name: name,
                InventoryNumber: inventory,
                OperatingSystemId: osId,
                WorkstationTypeId: typeId,
                OwnerId: null,
                OwnerDisplayName: null,
                OwnerDepartment: null,
                LocationId: locationId,
                LocationNote: null,
                Cpu: "CPU",
                Ram: "16 GB",
                Storage: "512 GB",
                MacAddress: Guid.NewGuid().ToString("N").Substring(0, 12),
                PurchasedAt: null,
                SupportUntil: null,
                PrimaryAdministratorId: null,
                SecondaryAdministratorId: null,
                Notes: null,
                NetworkAssignments: Array.Empty<NetworkEndpointDto>());

            var response = await Client.PostAsJsonAsync("/api/workstations", request);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<Workstation>();
            Assert.NotNull(created);
            return created!.Id;
        }

        var laptopId = await CreateWorkstationAsync("Alpha Laptop", "WRK-A-01", osWindows, typeLaptop, locationHq);
        var thinId = await CreateWorkstationAsync("Beta Thin", "WRK-T-01", osLinux, typeThin, locationRemote);

        var retireResponse = await Client.PostAsync($"/api/workstations/{thinId}/retire", null);
        retireResponse.EnsureSuccessStatusCode();

        var searchResponse = await Client.GetFromJsonAsync<PagedResponse<Workstation>>($"/api/workstations?page=1&size=50&search=Alpha");
        Assert.NotNull(searchResponse);
        Assert.Single(searchResponse!.Items);
        Assert.Equal(laptopId, searchResponse.Items[0].Id);

        var statusResponse = await Client.GetFromJsonAsync<PagedResponse<Workstation>>($"/api/workstations?page=1&size=50&status=Retired");
        Assert.NotNull(statusResponse);
        Assert.Single(statusResponse!.Items);
        Assert.Equal(thinId, statusResponse.Items[0].Id);

        var osResponse = await Client.GetFromJsonAsync<PagedResponse<Workstation>>($"/api/workstations?page=1&size=50&operatingSystemId={osLinux}");
        Assert.NotNull(osResponse);
        Assert.Single(osResponse!.Items);
        Assert.Equal(thinId, osResponse.Items[0].Id);

        var typeResponse = await Client.GetFromJsonAsync<PagedResponse<Workstation>>($"/api/workstations?page=1&size=50&workstationTypeId={typeLaptop}");
        Assert.NotNull(typeResponse);
        Assert.Single(typeResponse!.Items);
        Assert.Equal(laptopId, typeResponse.Items[0].Id);

        var locationResponse = await Client.GetFromJsonAsync<PagedResponse<Workstation>>($"/api/workstations?page=1&size=50&locationId={locationRemote}");
        Assert.NotNull(locationResponse);
        Assert.Single(locationResponse!.Items);
        Assert.Equal(thinId, locationResponse.Items[0].Id);
    }

    private sealed record PagedResponse<T>(T[] Items, int TotalCount, int Page, int PageSize);
}
