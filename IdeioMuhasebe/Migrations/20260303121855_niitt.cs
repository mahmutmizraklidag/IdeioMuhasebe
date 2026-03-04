using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdeioMuhasebe.Migrations
{
    /// <inheritdoc />
    public partial class niitt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Debts_DebtTypes_DebtTypeId",
                table: "Debts");

            migrationBuilder.DropIndex(
                name: "IX_Debts_DebtTypeId",
                table: "Debts");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Debts_DebtTypeId_IsPaid_DueDate",
                table: "Debts",
                columns: new[] { "DebtTypeId", "IsPaid", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Debts_DueDate",
                table: "Debts",
                column: "DueDate");

            migrationBuilder.AddForeignKey(
                name: "FK_Debts_DebtTypes_DebtTypeId",
                table: "Debts",
                column: "DebtTypeId",
                principalTable: "DebtTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Debts_DebtTypes_DebtTypeId",
                table: "Debts");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Debts_DebtTypeId_IsPaid_DueDate",
                table: "Debts");

            migrationBuilder.DropIndex(
                name: "IX_Debts_DueDate",
                table: "Debts");

            migrationBuilder.CreateIndex(
                name: "IX_Debts_DebtTypeId",
                table: "Debts",
                column: "DebtTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Debts_DebtTypes_DebtTypeId",
                table: "Debts",
                column: "DebtTypeId",
                principalTable: "DebtTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
