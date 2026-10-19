using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSocialBankDirectGroupFund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Groups",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.CreateTable(
                name: "FriendRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddresseeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LowerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HigherUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FriendRequests_Users_AddresseeUserId",
                        column: x => x.AddresseeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendRequests_Users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FundRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AmountPerMember = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundRequests_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupActivities_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupActivities_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupDeletionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FundBalanceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReadyAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupDeletionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupDeletionRequests_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupDeletionRequests_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupExpenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategorySnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PaidByType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaidByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupExpenses_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupExpenses_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupExpenses_Users_PaidByUserId",
                        column: x => x.PaidByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupFundRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TargetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupFundRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupFundRounds_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupFundRounds_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupReceivingBanks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankBrandSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    MaskedAccountSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountHolderSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupReceivingBanks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupReceivingBanks_BankConnections_BankConnectionId",
                        column: x => x.BankConnectionId,
                        principalTable: "BankConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupReceivingBanks_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupReceivingBanks_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Friendships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LowerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HigherUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friendships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Friendships_FriendRequests_AcceptedRequestId",
                        column: x => x.AcceptedRequestId,
                        principalTable: "FriendRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FundRequestRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundRequestRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundRequestRecipients_FundRequests_FundRequestId",
                        column: x => x.FundRequestId,
                        principalTable: "FundRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundRequestRecipients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupDeletionVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupDeletionRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VotedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupDeletionVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupDeletionVotes_GroupDeletionRequests_GroupDeletionRequestId",
                        column: x => x.GroupDeletionRequestId,
                        principalTable: "GroupDeletionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupDeletionVotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupRefunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupDeletionRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ContributionBasis = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RecipientBankConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupRefunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupRefunds_BankConnections_RecipientBankConnectionId",
                        column: x => x.RecipientBankConnectionId,
                        principalTable: "BankConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupRefunds_GroupDeletionRequests_GroupDeletionRequestId",
                        column: x => x.GroupDeletionRequestId,
                        principalTable: "GroupDeletionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupRefunds_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupFundRoundMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupFundRoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    RequiredAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupFundRoundMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupFundRoundMembers_GroupFundRounds_GroupFundRoundId",
                        column: x => x.GroupFundRoundId,
                        principalTable: "GroupFundRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupFundRoundMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FriendPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FriendshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nickname = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    IsFavorite = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FriendPreferences_Friendships_FriendshipId",
                        column: x => x.FriendshipId,
                        principalTable: "Friendships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FriendPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankDirectPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecipientDisplayNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    RecipientPhoneSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RecipientBankConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankBrandSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AccountHolderSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    MaskedAccountSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ClientRequestKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContextType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FundRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FundRequestRecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FundRoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FundRoundMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GroupRefundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SenderConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MatchedBankTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankDirectPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankDirectPayments_BankConnections_RecipientBankConnectionId",
                        column: x => x.RecipientBankConnectionId,
                        principalTable: "BankConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDirectPayments_BankTransactions_MatchedBankTransactionId",
                        column: x => x.MatchedBankTransactionId,
                        principalTable: "BankTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDirectPayments_FundRequestRecipients_FundRequestRecipientId",
                        column: x => x.FundRequestRecipientId,
                        principalTable: "FundRequestRecipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDirectPayments_FundRequests_FundRequestId",
                        column: x => x.FundRequestId,
                        principalTable: "FundRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDirectPayments_GroupDeletionRequests_DeletionRequestId",
                        column: x => x.DeletionRequestId,
                        principalTable: "GroupDeletionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDirectPayments_GroupFundRoundMembers_FundRoundMemberId",
                        column: x => x.FundRoundMemberId,
                        principalTable: "GroupFundRoundMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDirectPayments_GroupFundRounds_FundRoundId",
                        column: x => x.FundRoundId,
                        principalTable: "GroupFundRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDirectPayments_GroupRefunds_GroupRefundId",
                        column: x => x.GroupRefundId,
                        principalTable: "GroupRefunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDirectPayments_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDirectPayments_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDirectPayments_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FundTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReferenceCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundTransactions_BankDirectPayments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "BankDirectPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundTransactions_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvites_GroupId_InvitedUserId",
                table: "GroupInvites",
                columns: new[] { "GroupId", "InvitedUserId" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_BankDirectPayments_DeletionRequestId",
                table: "BankDirectPayments",
                column: "DeletionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDirectPayments_FundRequestId",
                table: "BankDirectPayments",
                column: "FundRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDirectPayments_FundRequestRecipientId",
                table: "BankDirectPayments",
                column: "FundRequestRecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDirectPayments_FundRoundId",
                table: "BankDirectPayments",
                column: "FundRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDirectPayments_FundRoundMemberId",
                table: "BankDirectPayments",
                column: "FundRoundMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDirectPayments_GroupId",
                table: "BankDirectPayments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDirectPayments_GroupRefundId",
                table: "BankDirectPayments",
                column: "GroupRefundId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDirectPayments_MatchedBankTransactionId",
                table: "BankDirectPayments",
                column: "MatchedBankTransactionId",
                unique: true,
                filter: "[MatchedBankTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankDirectPayments_PaymentReference",
                table: "BankDirectPayments",
                column: "PaymentReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankDirectPayments_RecipientBankConnectionId_Status_Amount",
                table: "BankDirectPayments",
                columns: new[] { "RecipientBankConnectionId", "Status", "Amount" });

            migrationBuilder.CreateIndex(
                name: "IX_BankDirectPayments_RecipientUserId",
                table: "BankDirectPayments",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDirectPayments_SenderUserId_ContextType_ClientRequestKey",
                table: "BankDirectPayments",
                columns: new[] { "SenderUserId", "ContextType", "ClientRequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FriendPreferences_FriendshipId_UserId",
                table: "FriendPreferences",
                columns: new[] { "FriendshipId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FriendPreferences_UserId",
                table: "FriendPreferences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_AddresseeUserId",
                table: "FriendRequests",
                column: "AddresseeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_LowerUserId_HigherUserId",
                table: "FriendRequests",
                columns: new[] { "LowerUserId", "HigherUserId" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_RequesterUserId",
                table: "FriendRequests",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_AcceptedRequestId",
                table: "Friendships",
                column: "AcceptedRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_LowerUserId_HigherUserId",
                table: "Friendships",
                columns: new[] { "LowerUserId", "HigherUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundRequestRecipients_FundRequestId_UserId",
                table: "FundRequestRecipients",
                columns: new[] { "FundRequestId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundRequestRecipients_UserId",
                table: "FundRequestRecipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FundRequests_CreatedByUserId",
                table: "FundRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FundRequests_GroupId",
                table: "FundRequests",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_GroupId",
                table: "FundTransactions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_PaymentId",
                table: "FundTransactions",
                column: "PaymentId",
                unique: true,
                filter: "[PaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_UserId",
                table: "FundTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupActivities_ActorUserId",
                table: "GroupActivities",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupActivities_GroupId_CreatedAt",
                table: "GroupActivities",
                columns: new[] { "GroupId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupDeletionRequests_GroupId",
                table: "GroupDeletionRequests",
                column: "GroupId",
                unique: true,
                filter: "[Status] IN ('Pending', 'Ready')");

            migrationBuilder.CreateIndex(
                name: "IX_GroupDeletionRequests_OwnerUserId",
                table: "GroupDeletionRequests",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupDeletionVotes_GroupDeletionRequestId_UserId",
                table: "GroupDeletionVotes",
                columns: new[] { "GroupDeletionRequestId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupDeletionVotes_UserId",
                table: "GroupDeletionVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupExpenses_CategoryId",
                table: "GroupExpenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupExpenses_GroupId",
                table: "GroupExpenses",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupExpenses_PaidByUserId",
                table: "GroupExpenses",
                column: "PaidByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFundRoundMembers_GroupFundRoundId_UserId",
                table: "GroupFundRoundMembers",
                columns: new[] { "GroupFundRoundId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupFundRoundMembers_UserId",
                table: "GroupFundRoundMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFundRounds_CreatedByUserId",
                table: "GroupFundRounds",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFundRounds_GroupId",
                table: "GroupFundRounds",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupReceivingBanks_BankConnectionId",
                table: "GroupReceivingBanks",
                column: "BankConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupReceivingBanks_GroupId",
                table: "GroupReceivingBanks",
                column: "GroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupReceivingBanks_OwnerUserId",
                table: "GroupReceivingBanks",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupRefunds_GroupDeletionRequestId_UserId",
                table: "GroupRefunds",
                columns: new[] { "GroupDeletionRequestId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupRefunds_RecipientBankConnectionId",
                table: "GroupRefunds",
                column: "RecipientBankConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupRefunds_UserId",
                table: "GroupRefunds",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FriendPreferences");

            migrationBuilder.DropTable(
                name: "FundTransactions");

            migrationBuilder.DropTable(
                name: "GroupActivities");

            migrationBuilder.DropTable(
                name: "GroupDeletionVotes");

            migrationBuilder.DropTable(
                name: "GroupExpenses");

            migrationBuilder.DropTable(
                name: "GroupReceivingBanks");

            migrationBuilder.DropTable(
                name: "Friendships");

            migrationBuilder.DropTable(
                name: "BankDirectPayments");

            migrationBuilder.DropTable(
                name: "FriendRequests");

            migrationBuilder.DropTable(
                name: "FundRequestRecipients");

            migrationBuilder.DropTable(
                name: "GroupFundRoundMembers");

            migrationBuilder.DropTable(
                name: "GroupRefunds");

            migrationBuilder.DropTable(
                name: "FundRequests");

            migrationBuilder.DropTable(
                name: "GroupFundRounds");

            migrationBuilder.DropTable(
                name: "GroupDeletionRequests");

            migrationBuilder.DropIndex(
                name: "IX_GroupInvites_GroupId_InvitedUserId",
                table: "GroupInvites");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Groups");
        }
    }
}
