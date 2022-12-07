using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAdapter.Migrations
{
    public partial class AddUserInfoFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "forums_info",
                table: "users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "logs_origin_info",
                table: "users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "forums_info",
                table: "users");

            migrationBuilder.DropColumn(
                name: "logs_origin_info",
                table: "users");
        }
    }
}
