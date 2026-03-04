using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdeioMuhasebe.Migrations
{
    /// <inheritdoc />
    public partial class statss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NetAmount",
                table: "RecurringIncomes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "RecurringIncomes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAmount",
                table: "Incomes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "Incomes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NetAmount",
                table: "RecurringIncomes");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "RecurringIncomes");

            migrationBuilder.DropColumn(
                name: "NetAmount",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "Incomes");
        }
    }
}
