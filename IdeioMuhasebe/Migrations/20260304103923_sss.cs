using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdeioMuhasebe.Migrations
{
    /// <inheritdoc />
    public partial class sss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecurringIncomeId",
                table: "Incomes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecurringIncomes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncomeTypeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Payer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DayOfMonth = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringIncomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringIncomes_IncomeTypes_IncomeTypeId",
                        column: x => x.IncomeTypeId,
                        principalTable: "IncomeTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_RecurringIncomeId",
                table: "Incomes",
                column: "RecurringIncomeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringIncomes_IncomeTypeId",
                table: "RecurringIncomes",
                column: "IncomeTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incomes_RecurringIncomes_RecurringIncomeId",
                table: "Incomes",
                column: "RecurringIncomeId",
                principalTable: "RecurringIncomes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incomes_RecurringIncomes_RecurringIncomeId",
                table: "Incomes");

            migrationBuilder.DropTable(
                name: "RecurringIncomes");

            migrationBuilder.DropIndex(
                name: "IX_Incomes_RecurringIncomeId",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "RecurringIncomeId",
                table: "Incomes");
        }
    }
}
