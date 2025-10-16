using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HWInventory.Infrastructure.Migrations;

public partial class AddSecurityMetricSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SecurityMetricSnapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                TotalUsers = table.Column<int>(type: "int", nullable: false),
                ActiveUsers = table.Column<int>(type: "int", nullable: false),
                TotpEnabled = table.Column<int>(type: "int", nullable: false),
                TotpRequired = table.Column<int>(type: "int", nullable: false),
                LockedOut = table.Column<int>(type: "int", nullable: false),
                PendingPasswordResets = table.Column<int>(type: "int", nullable: false),
                ActiveSessions = table.Column<int>(type: "int", nullable: false),
                AlertsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SecurityMetricSnapshots", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SecurityMetricSnapshots_CapturedAtUtc",
            table: "SecurityMetricSnapshots",
            column: "CapturedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SecurityMetricSnapshots");
    }
}
