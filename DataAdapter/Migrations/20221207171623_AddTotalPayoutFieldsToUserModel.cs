using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAdapter.Migrations
{
    public partial class AddTotalPayoutFieldsToUserModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "total_payout_count",
                table: "users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "total_payout_summary",
                table: "users",
                type: "double",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "total_payout_count",
                table: "users");

            migrationBuilder.DropColumn(
                name: "total_payout_summary",
                table: "users");
        }
    }
}
