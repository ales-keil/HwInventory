using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HWInventory.Infrastructure.Migrations;

public partial class AddReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ReportDefinitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Scope = table.Column<int>(type: "int", nullable: false),
                Format = table.Column<int>(type: "int", nullable: false),
                Recurrence = table.Column<int>(type: "int", nullable: false),
                RunAtTime = table.Column<TimeSpan>(type: "time", nullable: true),
                RunOnDayOfWeek = table.Column<int>(type: "int", nullable: true),
                RunOnDayOfMonth = table.Column<int>(type: "int", nullable: true),
                FilterJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                Recipients = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                StoragePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Enabled = table.Column<bool>(type: "bit", nullable: false),
                NextRunAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastRunAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReportDefinitions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ReportRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReportDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                ArtifactPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                DownloadToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReportRuns", x => x.Id);
                table.ForeignKey(
                    name: "FK_ReportRuns_ReportDefinitions_ReportDefinitionId",
                    column: x => x.ReportDefinitionId,
                    principalTable: "ReportDefinitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ReportRuns_ReportDefinitionId",
            table: "ReportRuns",
            column: "ReportDefinitionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ReportRuns");

        migrationBuilder.DropTable(
            name: "ReportDefinitions");
    }
}
