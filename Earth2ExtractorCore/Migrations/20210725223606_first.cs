using Microsoft.EntityFrameworkCore.Migrations;

namespace Earth2ExtractorCore.Migrations
{
    public partial class first : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LandFields",
                columns: table => new
                {
                    id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    tileClass = table.Column<byte>(type: "tinyint", nullable: false),
                    tileCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandFields", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    time = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    ownerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    previousOwnerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LandFieldid = table.Column<string>(type: "nvarchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_Transactions_LandFields_LandFieldid",
                        column: x => x.LandFieldid,
                        principalTable: "LandFields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_LandFieldid",
                table: "Transactions",
                column: "LandFieldid");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "LandFields");
        }
    }
}
