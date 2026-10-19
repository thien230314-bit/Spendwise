using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackendCorrectnessRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentInvestigations");

            migrationBuilder.DropTable(
                name: "SecurityListEntries");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_SenderUserId_IdempotencyKey",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_LedgerTransactions_BusinessType_IdempotencyKey",
                table: "LedgerTransactions");

            migrationBuilder.DropIndex(
                name: "IX_GroupContributions_UserId_IdempotencyKey",
                table: "GroupContributions");

            migrationBuilder.DropIndex(
                name: "IX_DebtSettlements_GroupDebtId",
                table: "DebtSettlements");

            migrationBuilder.DropIndex(
                name: "IX_DebtSettlements_IdempotencyKey",
                table: "DebtSettlements");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyScope",
                table: "Transfers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "SandboxBankTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WalletId",
                table: "SandboxBankTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyScope",
                table: "LedgerTransactions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_SenderUserId_IdempotencyScope_IdempotencyKey",
                table: "Transfers",
                columns: new[] { "SenderUserId", "IdempotencyScope", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SandboxBankTransactions_SandboxBankAccountId_IdempotencyKey",
                table: "SandboxBankTransactions",
                columns: new[] { "SandboxBankAccountId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_BusinessType_IdempotencyScope_IdempotencyKey",
                table: "LedgerTransactions",
                columns: new[] { "BusinessType", "IdempotencyScope", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContributions_GroupId_UserId_IdempotencyKey",
                table: "GroupContributions",
                columns: new[] { "GroupId", "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebtSettlements_GroupDebtId_IdempotencyKey",
                table: "DebtSettlements",
                columns: new[] { "GroupDebtId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM [SystemConfigurations] WHERE [Key] = N'TransferMinimum')
                    INSERT INTO [SystemConfigurations] ([Id], [CreatedAt], [Key], [Value], [ValueType], [Description]) VALUES ('00000000-0000-0000-0001-000000000001', SYSUTCDATETIME(), N'TransferMinimum', N'1000', N'decimal', N'SpendWise TransferMinimum setting');
                IF NOT EXISTS (SELECT 1 FROM [SystemConfigurations] WHERE [Key] = N'TransferMaximum')
                    INSERT INTO [SystemConfigurations] ([Id], [CreatedAt], [Key], [Value], [ValueType], [Description]) VALUES ('00000000-0000-0000-0001-000000000002', SYSUTCDATETIME(), N'TransferMaximum', N'50000000', N'decimal', N'SpendWise TransferMaximum setting');
                IF NOT EXISTS (SELECT 1 FROM [SystemConfigurations] WHERE [Key] = N'DailyTransferLimit')
                    INSERT INTO [SystemConfigurations] ([Id], [CreatedAt], [Key], [Value], [ValueType], [Description]) VALUES ('00000000-0000-0000-0001-000000000003', SYSUTCDATETIME(), N'DailyTransferLimit', N'100000000', N'decimal', N'SpendWise DailyTransferLimit setting');
                IF NOT EXISTS (SELECT 1 FROM [SystemConfigurations] WHERE [Key] = N'DepositMaximum')
                    INSERT INTO [SystemConfigurations] ([Id], [CreatedAt], [Key], [Value], [ValueType], [Description]) VALUES ('00000000-0000-0000-0001-000000000004', SYSUTCDATETIME(), N'DepositMaximum', N'100000000', N'decimal', N'SpendWise DepositMaximum setting');
                IF NOT EXISTS (SELECT 1 FROM [SystemConfigurations] WHERE [Key] = N'WithdrawalMaximum')
                    INSERT INTO [SystemConfigurations] ([Id], [CreatedAt], [Key], [Value], [ValueType], [Description]) VALUES ('00000000-0000-0000-0001-000000000005', SYSUTCDATETIME(), N'WithdrawalMaximum', N'50000000', N'decimal', N'SpendWise WithdrawalMaximum setting');
                IF NOT EXISTS (SELECT 1 FROM [SystemConfigurations] WHERE [Key] = N'RiskReviewThreshold')
                    INSERT INTO [SystemConfigurations] ([Id], [CreatedAt], [Key], [Value], [ValueType], [Description]) VALUES ('00000000-0000-0000-0001-000000000006', SYSUTCDATETIME(), N'RiskReviewThreshold', N'70', N'decimal', N'SpendWise RiskReviewThreshold setting');

                IF NOT EXISTS (SELECT 1 FROM [FeatureFlags] WHERE [Name] = N'P2PTransfer')
                    INSERT INTO [FeatureFlags] ([Id], [CreatedAt], [Name], [IsEnabled], [Description]) VALUES ('00000000-0000-0000-0002-000000000001', SYSUTCDATETIME(), N'P2PTransfer', 1, N'Enables P2PTransfer');
                IF NOT EXISTS (SELECT 1 FROM [FeatureFlags] WHERE [Name] = N'GroupFund')
                    INSERT INTO [FeatureFlags] ([Id], [CreatedAt], [Name], [IsEnabled], [Description]) VALUES ('00000000-0000-0000-0002-000000000002', SYSUTCDATETIME(), N'GroupFund', 1, N'Enables GroupFund');
                IF NOT EXISTS (SELECT 1 FROM [FeatureFlags] WHERE [Name] = N'MoneyRequest')
                    INSERT INTO [FeatureFlags] ([Id], [CreatedAt], [Name], [IsEnabled], [Description]) VALUES ('00000000-0000-0000-0002-000000000003', SYSUTCDATETIME(), N'MoneyRequest', 1, N'Enables MoneyRequest');
                IF NOT EXISTS (SELECT 1 FROM [FeatureFlags] WHERE [Name] = N'AIAssistant')
                    INSERT INTO [FeatureFlags] ([Id], [CreatedAt], [Name], [IsEnabled], [Description]) VALUES ('00000000-0000-0000-0002-000000000004', SYSUTCDATETIME(), N'AIAssistant', 1, N'Enables AIAssistant');
                IF NOT EXISTS (SELECT 1 FROM [FeatureFlags] WHERE [Name] = N'AIWriteActions')
                    INSERT INTO [FeatureFlags] ([Id], [CreatedAt], [Name], [IsEnabled], [Description]) VALUES ('00000000-0000-0000-0002-000000000005', SYSUTCDATETIME(), N'AIWriteActions', 0, N'AI write actions are not implemented');
                IF NOT EXISTS (SELECT 1 FROM [FeatureFlags] WHERE [Name] = N'BankWithdrawal')
                    INSERT INTO [FeatureFlags] ([Id], [CreatedAt], [Name], [IsEnabled], [Description]) VALUES ('00000000-0000-0000-0002-000000000006', SYSUTCDATETIME(), N'BankWithdrawal', 1, N'Enables BankWithdrawal');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [FeatureFlags] WHERE [Id] IN (
                    '00000000-0000-0000-0002-000000000001', '00000000-0000-0000-0002-000000000002',
                    '00000000-0000-0000-0002-000000000003', '00000000-0000-0000-0002-000000000004',
                    '00000000-0000-0000-0002-000000000005', '00000000-0000-0000-0002-000000000006');
                DELETE FROM [SystemConfigurations] WHERE [Id] IN (
                    '00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0001-000000000002',
                    '00000000-0000-0000-0001-000000000003', '00000000-0000-0000-0001-000000000004',
                    '00000000-0000-0000-0001-000000000005', '00000000-0000-0000-0001-000000000006');
                """);

            migrationBuilder.DropIndex(
                name: "IX_Transfers_SenderUserId_IdempotencyScope_IdempotencyKey",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_SandboxBankTransactions_SandboxBankAccountId_IdempotencyKey",
                table: "SandboxBankTransactions");

            migrationBuilder.DropIndex(
                name: "IX_LedgerTransactions_BusinessType_IdempotencyScope_IdempotencyKey",
                table: "LedgerTransactions");

            migrationBuilder.DropIndex(
                name: "IX_GroupContributions_GroupId_UserId_IdempotencyKey",
                table: "GroupContributions");

            migrationBuilder.DropIndex(
                name: "IX_DebtSettlements_GroupDebtId_IdempotencyKey",
                table: "DebtSettlements");

            migrationBuilder.DropColumn(
                name: "IdempotencyScope",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "SandboxBankTransactions");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "SandboxBankTransactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyScope",
                table: "LedgerTransactions");

            migrationBuilder.CreateTable(
                name: "PaymentInvestigations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Issue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LinkedTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentInvestigations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityListEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValueHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityListEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_SenderUserId_IdempotencyKey",
                table: "Transfers",
                columns: new[] { "SenderUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_BusinessType_IdempotencyKey",
                table: "LedgerTransactions",
                columns: new[] { "BusinessType", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContributions_UserId_IdempotencyKey",
                table: "GroupContributions",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebtSettlements_GroupDebtId",
                table: "DebtSettlements",
                column: "GroupDebtId");

            migrationBuilder.CreateIndex(
                name: "IX_DebtSettlements_IdempotencyKey",
                table: "DebtSettlements",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityListEntries_Type_Kind_ValueHash",
                table: "SecurityListEntries",
                columns: new[] { "Type", "Kind", "ValueHash" },
                unique: true);
        }
    }
}
