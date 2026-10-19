using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSwBankSandboxWithSePayBankHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Giữ ledger/history bất biến nhưng đổi tài khoản giả cũ thành ExternalSource trung tính trước khi bỏ enum runtime.
            migrationBuilder.Sql(
                """
                UPDATE [LedgerAccounts]
                SET [Type] = N'ExternalSource',
                    [Code] = CONCAT(N'LEGACYEXT:', CONVERT(nvarchar(36), [Id])),
                    [Name] = N'Legacy external source',
                    [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Type] = N'SandboxBank';

                DELETE FROM [SystemConfigurations]
                WHERE [Key] IN (N'DepositMaximum', N'WithdrawalMaximum');

                DELETE FROM [FeatureFlags]
                WHERE [Name] = N'BankWithdrawal';
                """);

            migrationBuilder.DropTable(
                name: "PaymentRequests");

            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents");

            migrationBuilder.DropTable(
                name: "ReconciliationIssues");

            migrationBuilder.DropTable(
                name: "SandboxBankAccounts");

            migrationBuilder.DropTable(
                name: "SandboxBankTransactions");

            migrationBuilder.CreateTable(
                name: "BankConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExternalAccountId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BankBrandName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    AccountHolderName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    MaskedAccountNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnlinkedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankConnections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankIntegrationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExternalEventId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ExternalTransactionId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalBankAccountId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProcessingStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankIntegrationEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankLinkSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExternalLinkTokenXid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BankConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalBankAccountId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankLinkSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankLinkSessions_BankConnections_BankConnectionId",
                        column: x => x.BankConnectionId,
                        principalTable: "BankConnections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BankLinkSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReferenceCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PaymentCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Gateway = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankTransactions_BankConnections_BankConnectionId",
                        column: x => x.BankConnectionId,
                        principalTable: "BankConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_Provider_ExternalAccountId",
                table: "BankConnections",
                columns: new[] { "Provider", "ExternalAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_UserId",
                table: "BankConnections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankIntegrationEvents_ExternalTransactionId",
                table: "BankIntegrationEvents",
                column: "ExternalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankIntegrationEvents_ProcessingStatus",
                table: "BankIntegrationEvents",
                column: "ProcessingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BankIntegrationEvents_Provider_ExternalEventId",
                table: "BankIntegrationEvents",
                columns: new[] { "Provider", "ExternalEventId" },
                unique: true,
                filter: "[ExternalEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankIntegrationEvents_ReceivedAt",
                table: "BankIntegrationEvents",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BankLinkSessions_BankConnectionId",
                table: "BankLinkSessions",
                column: "BankConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankLinkSessions_Provider_ExternalLinkTokenXid",
                table: "BankLinkSessions",
                columns: new[] { "Provider", "ExternalLinkTokenXid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankLinkSessions_UserId",
                table: "BankLinkSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_BankConnectionId",
                table: "BankTransactions",
                column: "BankConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_Provider_ExternalTransactionId",
                table: "BankTransactions",
                columns: new[] { "Provider", "ExternalTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_TransactionDate",
                table: "BankTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_UserId",
                table: "BankTransactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankIntegrationEvents");

            migrationBuilder.DropTable(
                name: "BankLinkSessions");

            migrationBuilder.DropTable(
                name: "BankTransactions");

            migrationBuilder.DropTable(
                name: "BankConnections");

            migrationBuilder.CreateTable(
                name: "PaymentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QrPayload = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SandboxBankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PaymentRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PaymentRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SandboxTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationIssues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SandboxBankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LedgerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SandboxBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SandboxBankAccounts_LedgerAccounts_LedgerAccountId",
                        column: x => x.LedgerAccountId,
                        principalTable: "LedgerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SandboxBankTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LedgerTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(130)", maxLength: 130, nullable: false),
                    SandboxBankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SandboxBankTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_Reference",
                table: "PaymentRequests",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_UserId_IdempotencyKey",
                table: "PaymentRequests",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_EventId",
                table: "PaymentWebhookEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SandboxBankAccounts_AccountNumber",
                table: "SandboxBankAccounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SandboxBankAccounts_LedgerAccountId",
                table: "SandboxBankAccounts",
                column: "LedgerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SandboxBankTransactions_Reference",
                table: "SandboxBankTransactions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SandboxBankTransactions_SandboxBankAccountId_IdempotencyKey",
                table: "SandboxBankTransactions",
                columns: new[] { "SandboxBankAccountId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            // Khôi phục các khóa vận hành của schema cũ khi rollback migration thay thế.
            migrationBuilder.Sql(
                """
                UPDATE ledger
                SET ledger.[Type] = N'SandboxBank',
                    ledger.[Code] = CONCAT(N'SANDBANK:', REPLACE(CONVERT(nvarchar(36), ledger.[OwnerUserId]), N'-', N'')),
                    ledger.[Name] = CONCAT(N'SW Bank - ', users.[FullName]),
                    ledger.[UpdatedAt] = SYSUTCDATETIME()
                FROM [LedgerAccounts] AS ledger
                INNER JOIN [Users] AS users ON users.[Id] = ledger.[OwnerUserId]
                WHERE ledger.[Code] LIKE N'LEGACYEXT:%'
                  AND ledger.[Name] = N'Legacy external source';

                IF NOT EXISTS (SELECT 1 FROM [SystemConfigurations] WHERE [Key] = N'DepositMaximum')
                    INSERT INTO [SystemConfigurations] ([Id], [CreatedAt], [Key], [Value], [ValueType], [Description])
                    VALUES (NEWID(), SYSUTCDATETIME(), N'DepositMaximum', N'100000000', N'decimal', N'Legacy deposit maximum');
                IF NOT EXISTS (SELECT 1 FROM [SystemConfigurations] WHERE [Key] = N'WithdrawalMaximum')
                    INSERT INTO [SystemConfigurations] ([Id], [CreatedAt], [Key], [Value], [ValueType], [Description])
                    VALUES (NEWID(), SYSUTCDATETIME(), N'WithdrawalMaximum', N'50000000', N'decimal', N'Legacy withdrawal maximum');
                IF NOT EXISTS (SELECT 1 FROM [FeatureFlags] WHERE [Name] = N'BankWithdrawal')
                    INSERT INTO [FeatureFlags] ([Id], [CreatedAt], [Name], [IsEnabled], [Description])
                    VALUES (NEWID(), SYSUTCDATETIME(), N'BankWithdrawal', 1, N'Legacy bank withdrawal');
                """);
        }
    }
}
