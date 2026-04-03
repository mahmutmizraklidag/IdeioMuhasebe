using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdeioMuhasebe.Migrations
{
    /// <inheritdoc />
    public partial class initcurr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CarryForwardBalance",
                table: "Incomes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CarryForwardBalance",
                table: "Debts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarryForwardBalance",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "CarryForwardBalance",
                table: "Debts");
        }
    }
}
