using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAdapter.Migrations
{
    public partial class AddIsLoadedFlagToCheckings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_dublicates_filled_to_db",
                table: "wp_login_checks",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_dublicates_filled_to_db",
                table: "cpanel_whm_checks",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_dublicates_filled_to_db",
                table: "wp_login_checks");

            migrationBuilder.DropColumn(
                name: "is_dublicates_filled_to_db",
                table: "cpanel_whm_checks");
        }
    }
}
