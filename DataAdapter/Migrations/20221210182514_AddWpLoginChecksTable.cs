using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAdapter.Migrations
{
    public partial class AddWpLoginChecksTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wp_login_checks",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    status = table.Column<int>(type: "int", nullable: false),
                    checking_time = table.Column<TimeSpan>(type: "time(6)", nullable: false),
                    start_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    end_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    from_user_id = table.Column<long>(type: "bigint", nullable: false),
                    from_username = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    dublicate_founded_count = table.Column<int>(type: "int", nullable: false),
                    dublicate_founded_count_manual = table.Column<int>(type: "int", nullable: false),
                    shells_founded_count = table.Column<int>(type: "int", nullable: false),
                    shells_founded_count_manual = table.Column<int>(type: "int", nullable: false),
                    cpanels_reseted_founded_count = table.Column<int>(type: "int", nullable: false),
                    cpanels_reseted_founded_count_manual = table.Column<int>(type: "int", nullable: false),
                    smtps_founded_count = table.Column<int>(type: "int", nullable: false),
                    smtps_founded_count_manual = table.Column<int>(type: "int", nullable: false),
                    logged_wordpress_founded_count = table.Column<int>(type: "int", nullable: false),
                    logged_wordpress_founded_countmanual = table.Column<int>(type: "int", nullable: false),
                    dublicate_file_path = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    shells_file_path = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cpanels_file_path = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    smtps_file_path = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    logged_wordpress_file_path = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    original_file_path = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_manual_check_end = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wp_login_checks", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wp_login_checks");
        }
    }
}
