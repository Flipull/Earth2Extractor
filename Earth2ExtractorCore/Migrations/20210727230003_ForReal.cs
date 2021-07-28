using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Earth2ExtractorCore.Migrations
{
    public partial class ForReal : Migration
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
                name: "Simpletons",
                columns: table => new
                {
                    userid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    momenta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    tilesSoldAmount = table.Column<int>(type: "int", nullable: false),
                    tilesBoughtAmount = table.Column<int>(type: "int", nullable: false),
                    totalPropertiesOwned = table.Column<int>(type: "int", nullable: false),
                    totalPropertiesResold = table.Column<int>(type: "int", nullable: false),
                    currentPropertiesOwned = table.Column<int>(type: "int", nullable: false),
                    profitsOnSell = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    returnsOnSell = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Simpletons", x => new { x.userid, x.momenta });
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    locked = table.Column<bool>(type: "bit", nullable: false),
                    updated = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                    ownerId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    previousOwnerId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    landFieldid = table.Column<string>(type: "nvarchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_Transactions_LandFields_landFieldid",
                        column: x => x.landFieldid,
                        principalTable: "LandFields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_landFieldid",
                table: "Transactions",
                column: "landFieldid");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ownerId",
                table: "Transactions",
                column: "ownerId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_previousOwnerId",
                table: "Transactions",
                column: "previousOwnerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Simpletons");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "LandFields");
        }
    }
}
