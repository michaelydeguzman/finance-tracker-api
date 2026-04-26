using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RedesignRecurringTransactionDomainModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-09: Null out existing FrequencyId values before dropping the column.
            // Existing transactions become standalone (no recurring template).
            migrationBuilder.Sql(
                "UPDATE [Transactions] SET [FrequencyId] = NULL WHERE [FrequencyId] IS NOT NULL");

            // ... EF Core auto-generated DDL follows below (do not modify) ...
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Frequencies_FrequencyId",
                table: "Transactions");

            migrationBuilder.RenameColumn(
                name: "FrequencyId",
                table: "Transactions",
                newName: "RecurringTransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_FrequencyId",
                table: "Transactions",
                newName: "IX_Transactions_RecurringTransactionId");

            migrationBuilder.CreateTable(
                name: "RecurringTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DefaultAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrequencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextOccurrenceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringTransactions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringTransactions_Frequencies_FrequencyId",
                        column: x => x.FrequencyId,
                        principalTable: "Frequencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_CategoryId",
                table: "RecurringTransactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_FrequencyId",
                table: "RecurringTransactions",
                column: "FrequencyId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_Status",
                table: "RecurringTransactions",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_RecurringTransactions_RecurringTransactionId",
                table: "Transactions",
                column: "RecurringTransactionId",
                principalTable: "RecurringTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Note: FrequencyId values cannot be restored — Down() reverts schema only.
            // ... EF Core auto-generated DDL follows ...
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_RecurringTransactions_RecurringTransactionId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "RecurringTransactions");

            migrationBuilder.RenameColumn(
                name: "RecurringTransactionId",
                table: "Transactions",
                newName: "FrequencyId");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_RecurringTransactionId",
                table: "Transactions",
                newName: "IX_Transactions_FrequencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Frequencies_FrequencyId",
                table: "Transactions",
                column: "FrequencyId",
                principalTable: "Frequencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
