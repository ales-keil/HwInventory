using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HWInventory.Infrastructure.Migrations;

public partial class AddWorkstationHandovers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WorkstationHandovers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkstationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OldOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                OldOwnerDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OldOwnerDepartment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OldLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OldLocationName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OldLocationNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                NewOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                NewOwnerDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                NewOwnerDepartment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                NewLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                NewLocationName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                NewLocationNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                NewAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RequestedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                TokenExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                AcceptTokenHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                DeclineTokenHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Force = table.Column<bool>(type: "bit", nullable: false),
                EmailSent = table.Column<bool>(type: "bit", nullable: false),
                EmailError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CompletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ToRecipientsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CcRecipientsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                BccRecipientsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Subject = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                MessageBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Comment = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkstationHandovers", x => x.Id);
                table.ForeignKey(
                    name: "FK_WorkstationHandovers_Workstations_WorkstationId",
                    column: x => x.WorkstationId,
                    principalTable: "Workstations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WorkstationHandovers_Status",
            table: "WorkstationHandovers",
            columns: new[] { "WorkstationId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkstationHandovers_TokenExpiresAtUtc",
            table: "WorkstationHandovers",
            column: "TokenExpiresAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "WorkstationHandovers");
    }
}
