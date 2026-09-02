using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Households: a group of users who share one set of financial records.
    ///
    /// Nothing here backfills, and that is the point. Every existing row keeps a null
    /// HouseholdId, which under the widened tenancy filter means exactly what it meant
    /// before this migration — visible to its owner and to nobody else. Sharing begins only
    /// when somebody creates a household, and the application re-stamps their own rows then.
    /// </summary>
    /// <inheritdoc />
    public partial class AddHouseholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Households", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Households_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            foreach (var sharedTable in new[] { "Categories", "Transactions", "RecurringTransactions" })
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "HouseholdId",
                    table: sharedTable,
                    type: "uniqueidentifier",
                    nullable: true);
            }

            migrationBuilder.CreateIndex(
                name: "IX_Households_OwnerUserId",
                table: "Households",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_HouseholdId_Status",
                table: "HouseholdInvitations",
                columns: new[] { "HouseholdId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_InvitedByUserId",
                table: "HouseholdInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_InvitedEmail_Status",
                table: "HouseholdInvitations",
                columns: new[] { "InvitedEmail", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_HouseholdId",
                table: "Users",
                column: "HouseholdId");

            // Household-leading counterparts to the tenant-leading indexes added with
            // tenancy. The shared view runs the same queries as the private one, just
            // filtered on the other column.
            migrationBuilder.CreateIndex(
                name: "IX_Categories_HouseholdId_CategoryType",
                table: "Categories",
                columns: new[] { "HouseholdId", "CategoryType" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_HouseholdId_TransactionDate",
                table: "Transactions",
                columns: new[] { "HouseholdId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_HouseholdId_CreatedAt",
                table: "Transactions",
                columns: new[] { "HouseholdId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_HouseholdId_Status",
                table: "RecurringTransactions",
                columns: new[] { "HouseholdId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Households_HouseholdId",
                table: "Users",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Households_HouseholdId",
                table: "Categories",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Households_HouseholdId",
                table: "Transactions",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringTransactions_Households_HouseholdId",
                table: "RecurringTransactions",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecurringTransactions_Households_HouseholdId",
                table: "RecurringTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Households_HouseholdId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Households_HouseholdId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Households_HouseholdId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "HouseholdInvitations");

            migrationBuilder.DropTable(
                name: "Households");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_HouseholdId_Status",
                table: "RecurringTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_HouseholdId_CreatedAt",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_HouseholdId_TransactionDate",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Categories_HouseholdId_CategoryType",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Users_HouseholdId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "Users");
        }
    }
}
