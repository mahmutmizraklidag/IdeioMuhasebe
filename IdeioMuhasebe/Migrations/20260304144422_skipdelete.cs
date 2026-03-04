using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdeioMuhasebe.Migrations
{
    /// <inheritdoc />
    public partial class skipdelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringDebtSkips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecurringDebtId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringDebtSkips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringDebtSkips_RecurringDebts_RecurringDebtId",
                        column: x => x.RecurringDebtId,
                        principalTable: "RecurringDebts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecurringIncomeSkips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecurringIncomeId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringIncomeSkips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringIncomeSkips_RecurringIncomes_RecurringIncomeId",
                        column: x => x.RecurringIncomeId,
                        principalTable: "RecurringIncomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringDebtSkips_RecurringDebtId_Month",
                table: "RecurringDebtSkips",
                columns: new[] { "RecurringDebtId", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringIncomeSkips_RecurringIncomeId_Month",
                table: "RecurringIncomeSkips",
                columns: new[] { "RecurringIncomeId", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringDebtSkips");

            migrationBuilder.DropTable(
                name: "RecurringIncomeSkips");
        }
    }
}
