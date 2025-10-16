using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HWInventory.Infrastructure.Migrations
{
    public partial class AddImportJobs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    JobStatus = table.Column<int>(type: "int", nullable: false),
                    ConflictStrategy = table.Column<int>(type: "int", nullable: false),
                    DryRun = table.Column<bool>(type: "bit", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StoredFilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    MappingJson = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ResultLog = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ProcessedRows = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRows = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedRows = table.Column<long>(type: "bigint", nullable: true),
                    SkippedRows = table.Column<long>(type: "bigint", nullable: true),
                    SendEmail = table.Column<bool>(type: "bit", nullable: false),
                    EmailRecipients = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJobs", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportJobs");
        }
    }
}
