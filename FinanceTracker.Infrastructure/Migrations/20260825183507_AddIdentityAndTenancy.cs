using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAndTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_CreatedAt",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransactionDate",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_Status",
                table: "RecurringTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CategoryType",
                table: "Categories");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    EmailVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            // --- Tenancy backfill -------------------------------------------------
            // Existing rows predate multi-user support and have no owner. EF would add
            // UserId as NOT NULL defaulting to Guid.Empty, which the foreign key below
            // then rejects because no such user exists. So: seed the owner, add the
            // column nullable, backfill it, and only then tighten.
            //
            // CHECK BEFORE APPLYING: this address must match the Google account you sign
            // in with, or SSO will create a second account instead of adopting this one.
            var ownerId = new Guid("e71be760-2d95-4ee4-ba1d-b56be50329fc");
            var ownerEmail = "michaelydeguzman@gmail.com";
            var seededAt = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Email", "EmailVerifiedAt", "DisplayName", "Status", "CreatedAt" },
                values: new object[] { ownerId, ownerEmail, seededAt, "Owner", "Active", seededAt });

            foreach (var tenantTable in new[] { "Categories", "Transactions", "RecurringTransactions" })
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "UserId",
                    table: tenantTable,
                    type: "uniqueidentifier",
                    nullable: true);

                migrationBuilder.Sql(
                    $"UPDATE [{tenantTable}] SET [UserId] = '{ownerId}' WHERE [UserId] IS NULL;");

                migrationBuilder.AlterColumn<Guid>(
                    name: "UserId",
                    table: tenantTable,
                    type: "uniqueidentifier",
                    nullable: false,
                    oldClrType: typeof(Guid),
                    oldType: "uniqueidentifier",
                    oldNullable: true);
            }
            // ----------------------------------------------------------------------

            migrationBuilder.CreateTable(
                name: "UserCredentials",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCredentials", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserCredentials_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProviderSubject = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserIdentities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_CategoryId",
                table: "Transactions",
                columns: new[] { "UserId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_CreatedAt",
                table: "Transactions",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_TransactionDate",
                table: "Transactions",
                columns: new[] { "UserId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_Status_NextOccurrenceDate",
                table: "RecurringTransactions",
                columns: new[] { "Status", "NextOccurrenceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_UserId_Status",
                table: "RecurringTransactions",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId_CategoryType",
                table: "Categories",
                columns: new[] { "UserId", "CategoryType" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId_CategoryType_Name",
                table: "Categories",
                columns: new[] { "UserId", "CategoryType", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentities_Provider_ProviderSubject",
                table: "UserIdentities",
                columns: new[] { "Provider", "ProviderSubject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentities_UserId_Provider",
                table: "UserIdentities",
                columns: new[] { "UserId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_TokenHash",
                table: "UserTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_UserId_Purpose",
                table: "UserTokens",
                columns: new[] { "UserId", "Purpose" });

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Users_UserId",
                table: "Categories",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringTransactions_Users_UserId",
                table: "RecurringTransactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Users_UserId",
                table: "Transactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Users_UserId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringTransactions_Users_UserId",
                table: "RecurringTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Users_UserId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "UserCredentials");

            migrationBuilder.DropTable(
                name: "UserIdentities");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_UserId_CategoryId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_UserId_CreatedAt",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_UserId_TransactionDate",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_Status_NextOccurrenceDate",
                table: "RecurringTransactions");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_UserId_Status",
                table: "RecurringTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Categories_UserId_CategoryType",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_UserId_CategoryType_Name",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CreatedAt",
                table: "Transactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionDate",
                table: "Transactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_Status",
                table: "RecurringTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CategoryType",
                table: "Categories",
                column: "CategoryType");
        }
    }
}
