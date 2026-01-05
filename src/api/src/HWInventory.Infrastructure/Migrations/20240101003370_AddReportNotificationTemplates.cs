using Microsoft.EntityFrameworkCore.Migrations;

namespace HWInventory.Infrastructure.Migrations;

public partial class AddReportNotificationTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EmailBodyTemplate",
            table: "ReportDefinitions",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: false,
            defaultValue: "Report '{ReportName}' ({Scope}) byl {StatusText} v {CompletedAt}.\\n\\nSoubor: {ArtifactPath}\\nDetaily: {ReportUrl}");

        migrationBuilder.AddColumn<string>(
            name: "EmailSubjectTemplate",
            table: "ReportDefinitions",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "HW Inventory – report {ReportName}");

        migrationBuilder.AddColumn<bool>(
            name: "IncludeArtifactInEmail",
            table: "ReportDefinitions",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "NotifyOnFailureOnly",
            table: "ReportDefinitions",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EmailBodyTemplate",
            table: "ReportDefinitions");

        migrationBuilder.DropColumn(
            name: "EmailSubjectTemplate",
            table: "ReportDefinitions");

        migrationBuilder.DropColumn(
            name: "IncludeArtifactInEmail",
            table: "ReportDefinitions");

        migrationBuilder.DropColumn(
            name: "NotifyOnFailureOnly",
            table: "ReportDefinitions");
    }
}
