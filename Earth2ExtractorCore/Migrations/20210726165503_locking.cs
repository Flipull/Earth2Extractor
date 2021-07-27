using Microsoft.EntityFrameworkCore.Migrations;

namespace Earth2ExtractorCore.Migrations
{
    public partial class locking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "locked",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "locked",
                table: "Users");
        }
    }
}
