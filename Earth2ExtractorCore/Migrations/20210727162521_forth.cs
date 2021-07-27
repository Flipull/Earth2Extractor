using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Earth2ExtractorCore.Migrations
{
    public partial class forth : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Simpletons",
                columns: table => new
                {
                    Momenta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    userId = table.Column<string>(type: "nvarchar(36)", nullable: true),
                    tilesSoldAmount = table.Column<int>(type: "int", nullable: false),
                    tilesBoughtAmount = table.Column<int>(type: "int", nullable: false),
                    totalPropertiesOwned = table.Column<int>(type: "int", nullable: false),
                    totalPropertiesResold = table.Column<int>(type: "int", nullable: false),
                    currentPropertiesOwned = table.Column<int>(type: "int", nullable: false),
                    profitsOnSell = table.Column<int>(type: "int", nullable: false),
                    returnsOnSell = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Simpletons", x => x.Momenta);
                    table.ForeignKey(
                        name: "FK_Simpletons_Users_userId",
                        column: x => x.userId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Simpletons_userId",
                table: "Simpletons",
                column: "userId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Simpletons");
        }
    }
}
