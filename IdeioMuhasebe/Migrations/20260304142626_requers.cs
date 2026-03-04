using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdeioMuhasebe.Migrations
{
    /// <inheritdoc />
    public partial class requers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreateDate",
                table: "RecurringDebts");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "RecurringDebts");

            migrationBuilder.AddColumn<int>(
                name: "PeriodCount",
                table: "RecurringIncomes",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Payee",
                table: "RecurringDebts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "RecurringDebts",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AddColumn<int>(
                name: "PeriodCount",
                table: "RecurringDebts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurringDebtId",
                table: "Debts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Debts_RecurringDebtId",
                table: "Debts",
                column: "RecurringDebtId");

            migrationBuilder.AddForeignKey(
                name: "FK_Debts_RecurringDebts_RecurringDebtId",
                table: "Debts",
                column: "RecurringDebtId",
                principalTable: "RecurringDebts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Debts_RecurringDebts_RecurringDebtId",
                table: "Debts");

            migrationBuilder.DropIndex(
                name: "IX_Debts_RecurringDebtId",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "PeriodCount",
                table: "RecurringIncomes");

            migrationBuilder.DropColumn(
                name: "PeriodCount",
                table: "RecurringDebts");

            migrationBuilder.DropColumn(
                name: "RecurringDebtId",
                table: "Debts");

            migrationBuilder.AlterColumn<string>(
                name: "Payee",
                table: "RecurringDebts",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "RecurringDebts",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreateDate",
                table: "RecurringDebts",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "RecurringDebts",
                type: "datetime2",
                nullable: true);
        }
    }
}
