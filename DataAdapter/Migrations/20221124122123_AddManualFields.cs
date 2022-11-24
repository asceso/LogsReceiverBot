using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAdapter.Migrations
{
    public partial class AddManualFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CpanelBadCountManual",
                table: "ManualChecks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CpanelGoodCountManual",
                table: "ManualChecks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DublicateFoundedCountManual",
                table: "ManualChecks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsManualCheckEnd",
                table: "ManualChecks",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WebmailFoundedCountManual",
                table: "ManualChecks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WhmBadCountManual",
                table: "ManualChecks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WhmGoodCountManual",
                table: "ManualChecks",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CpanelBadCountManual",
                table: "ManualChecks");

            migrationBuilder.DropColumn(
                name: "CpanelGoodCountManual",
                table: "ManualChecks");

            migrationBuilder.DropColumn(
                name: "DublicateFoundedCountManual",
                table: "ManualChecks");

            migrationBuilder.DropColumn(
                name: "IsManualCheckEnd",
                table: "ManualChecks");

            migrationBuilder.DropColumn(
                name: "WebmailFoundedCountManual",
                table: "ManualChecks");

            migrationBuilder.DropColumn(
                name: "WhmBadCountManual",
                table: "ManualChecks");

            migrationBuilder.DropColumn(
                name: "WhmGoodCountManual",
                table: "ManualChecks");
        }
    }
}
