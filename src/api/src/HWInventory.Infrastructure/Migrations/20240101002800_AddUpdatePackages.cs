using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HWInventory.Infrastructure.Migrations
{
    public partial class AddUpdatePackages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UpdatePackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StoredPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    StagingPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ManifestJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdateStatus = table.Column<int>(type: "int", nullable: false),
                    PreserveDatabaseConfiguration = table.Column<bool>(type: "bit", nullable: false),
                    CreateBackupBeforeInstall = table.Column<bool>(type: "bit", nullable: false),
                    BackupStoragePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PerformIntegrityCheck = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedBackupAvailable = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    LogPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdatePackages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UpdatePackages_CreatedAtUtc",
                table: "UpdatePackages",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UpdatePackages_UpdateStatus",
                table: "UpdatePackages",
                column: "UpdateStatus");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpdatePackages");
        }
    }
}
