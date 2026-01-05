using Microsoft.EntityFrameworkCore.Migrations;

namespace HWInventory.Infrastructure.Migrations;

public partial class AddSsprRateLimiting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ClientIp",
            table: "PasswordResetTokens",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UserAgent",
            table: "PasswordResetTokens",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_PasswordResetTokens_AppUserId_CreatedAtUtc",
            table: "PasswordResetTokens",
            columns: new[] { "AppUserId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_PasswordResetTokens_ClientIp_CreatedAtUtc",
            table: "PasswordResetTokens",
            columns: new[] { "ClientIp", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PasswordResetTokens_AppUserId_CreatedAtUtc",
            table: "PasswordResetTokens");

        migrationBuilder.DropIndex(
            name: "IX_PasswordResetTokens_ClientIp_CreatedAtUtc",
            table: "PasswordResetTokens");

        migrationBuilder.DropColumn(
            name: "ClientIp",
            table: "PasswordResetTokens");

        migrationBuilder.DropColumn(
            name: "UserAgent",
            table: "PasswordResetTokens");
    }
}
