using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdeioMuhasebe.Migrations
{
    /// <inheritdoc />
    public partial class ggg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringDebts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DebtTypeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Payee = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DayOfMonth = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringDebts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringDebts_DebtTypes_DebtTypeId",
                        column: x => x.DebtTypeId,
                        principalTable: "DebtTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringDebts_DebtTypeId",
                table: "RecurringDebts",
                column: "DebtTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringDebts_IsActive_DebtTypeId",
                table: "RecurringDebts",
                columns: new[] { "IsActive", "DebtTypeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringDebts");
        }
    }
}
