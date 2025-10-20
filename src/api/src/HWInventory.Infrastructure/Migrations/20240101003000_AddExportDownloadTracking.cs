using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HWInventory.Infrastructure.Migrations;

public partial class AddExportDownloadTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DownloadCount",
            table: "ExportJobs",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastDownloadedAtUtc",
            table: "ExportJobs",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DownloadCount",
            table: "ExportJobs");

        migrationBuilder.DropColumn(
            name: "LastDownloadedAtUtc",
            table: "ExportJobs");
    }
}
