using System;
using System.Linq;
using System.Threading.Tasks;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using HWInventory.Infrastructure.Common;
using HWInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HWInventory.Infrastructure.Tests;

public class InventoryQueryFiltersTests
{
    [Fact]
    public async Task ApplyServerFilters_StatusEq_ReturnsOnlyMatching()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        context.Servers.AddRange(
            new Server
            {
                Name = "srv-active",
                InventoryNumber = "S-001",
                Manufacturer = "Dell",
                Model = "R750",
                EnvironmentId = Guid.NewGuid(),
                WsusPriorityId = Guid.NewGuid(),
                OperatingSystemId = Guid.NewGuid(),
                ServerRoleId = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                Status = EntityStatus.Active,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "tests"
            },
            new Server
            {
                Name = "srv-retired",
                InventoryNumber = "S-002",
                Manufacturer = "HP",
                Model = "DL380",
                EnvironmentId = Guid.NewGuid(),
                WsusPriorityId = Guid.NewGuid(),
                OperatingSystemId = Guid.NewGuid(),
                ServerRoleId = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                Status = EntityStatus.Retired,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "tests"
            });
        await context.SaveChangesAsync();

        var filters = InventoryQueryFilters.Parse("[{\"field\":\"status\",\"op\":\"eq\",\"value\":\"Active\"}]");
        var query = InventoryQueryFilters.ApplyServerFilters(context.Servers.AsQueryable(), filters);
        var results = await query.ToListAsync();

        Assert.Single(results);
        Assert.Equal("srv-active", results[0].Name);
    }

    [Fact]
    public async Task ApplyWorkstationFilters_DepartmentContains_FiltersCorrectly()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        var locationId = Guid.NewGuid();
        context.Workstations.AddRange(
            new Workstation
            {
                Name = "ws-alpha",
                InventoryNumber = "W-001",
                OperatingSystemId = Guid.NewGuid(),
                WorkstationTypeId = Guid.NewGuid(),
                LocationId = locationId,
                OwnerDepartment = "Finance",
                Status = EntityStatus.Active,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "tests"
            },
            new Workstation
            {
                Name = "ws-beta",
                InventoryNumber = "W-002",
                OperatingSystemId = Guid.NewGuid(),
                WorkstationTypeId = Guid.NewGuid(),
                LocationId = locationId,
                OwnerDepartment = "Operations",
                Status = EntityStatus.Active,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "tests"
            });
        await context.SaveChangesAsync();

        var filters = InventoryQueryFilters.Parse("[{\"field\":\"ownerDepartment\",\"op\":\"contains\",\"value\":\"Fin\"}]");
        var query = InventoryQueryFilters.ApplyWorkstationFilters(context.Workstations.AsQueryable(), filters);
        var results = await query.ToListAsync();

        Assert.Single(results);
        Assert.Equal("ws-alpha", results[0].Name);
    }

    [Fact]
    public void Parse_ObjectNotation_ProducesEqFilter()
    {
        var location = Guid.NewGuid();
        var json = $"{{\"locationId\":\"{location}\"}}";

        var filters = InventoryQueryFilters.Parse(json);

        Assert.Single(filters);
        var filter = filters[0];
        Assert.Equal("locationId", filter.Field);
        Assert.Equal("eq", filter.Operator);
        Assert.Single(filter.Values);
        Assert.Equal(location.ToString(), filter.Values[0]);
    }

    [Fact]
    public void ValidateForExportScope_InvalidField_ReturnsError()
    {
        var filters = InventoryQueryFilters.Parse("[{\"field\":\"unknown\",\"op\":\"eq\",\"value\":\"foo\"}]");

        var errors = InventoryQueryFilters.ValidateForExportScope(ExportScope.Servers, filters);

        Assert.Single(errors);
        Assert.Contains("unknown", errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateForExportScope_InvalidGuidValue_ReturnsError()
    {
        var filters = InventoryQueryFilters.Parse("[{\"field\":\"environmentId\",\"op\":\"eq\",\"value\":\"not-guid\"}]");

        var errors = InventoryQueryFilters.ValidateForExportScope(ExportScope.Servers, filters);

        Assert.Single(errors);
        Assert.Contains("environmentId", errors[0], StringComparison.OrdinalIgnoreCase);
    }
}
