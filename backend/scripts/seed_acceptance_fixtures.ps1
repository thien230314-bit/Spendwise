<#
.SYNOPSIS
    SpendWise Backend Acceptance Test Fixtures Seeder
.DESCRIPTION
    Seeds deterministic, FK-ordered fixtures for the 216 User Operations Newman acceptance suite.
    Safe for local testing against SQL Server (SQLEXPRESS02).
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File backend/scripts/seed_acceptance_fixtures.ps1
#>
param(
    [string]$Server = "DESKTOP-B5KA3LT\SQLEXPRESS02",
    [string]$Database = "SpendWise_E2E_G4"
)

$ErrorActionPreference = "Stop"

Write-Host "Seeding database fixtures for SpendWise acceptance test suite on $Server / $Database..."

$connStr = "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"



Add-Type -AssemblyName System.Data



$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)



$conn.Open()







# Clean up existing test fixture rows in correct foreign key order



$cleanupCmd = $conn.CreateCommand()



$cleanupCmd.CommandText = @"



SET QUOTED_IDENTIFIER ON;



SET ANSI_NULLS ON;







-- Election tables



IF OBJECT_ID('V1ElectionBallots', 'U') IS NOT NULL DELETE FROM V1ElectionBallots;



IF OBJECT_ID('V1ElectionCandidates', 'U') IS NOT NULL DELETE FROM V1ElectionCandidates;



IF OBJECT_ID('V1ElectionVoters', 'U') IS NOT NULL DELETE FROM V1ElectionVoters;



IF OBJECT_ID('V1Elections', 'U') IS NOT NULL DELETE FROM V1Elections;







-- Activity Proposals



IF OBJECT_ID('V1ActivityVoters', 'U') IS NOT NULL DELETE FROM V1ActivityVoters;



IF OBJECT_ID('V1ActivityProposals', 'U') IS NOT NULL DELETE FROM V1ActivityProposals;







-- Fund tables



IF OBJECT_ID('V1FundActivities', 'U') IS NOT NULL DELETE FROM V1FundActivities;



IF OBJECT_ID('V1FundObligations', 'U') IS NOT NULL DELETE FROM V1FundObligations;



IF OBJECT_ID('V1FundTreasuries', 'U') IS NOT NULL DELETE FROM V1FundTreasuries;



IF OBJECT_ID('V1FundContributions', 'U') IS NOT NULL DELETE FROM V1FundContributions;



IF OBJECT_ID('V1FundInvitations', 'U') IS NOT NULL DELETE FROM V1FundInvitations;



IF OBJECT_ID('V1FundRequests', 'U') IS NOT NULL DELETE FROM V1FundRequests;



IF OBJECT_ID('V1OwnershipOffers', 'U') IS NOT NULL DELETE FROM V1OwnershipOffers;



IF OBJECT_ID('V1FundMemberships', 'U') IS NOT NULL DELETE FROM V1FundMemberships;



IF OBJECT_ID('V1Funds', 'U') IS NOT NULL



BEGIN



    UPDATE V1Funds SET OwnerResponseCaseId = NULL;



    DELETE FROM V1Funds;



END







-- Legacy Group deletion & operations



IF OBJECT_ID('BankDirectPayments', 'U') IS NOT NULL DELETE FROM BankDirectPayments;



IF OBJECT_ID('GroupFundShares', 'U') IS NOT NULL DELETE FROM GroupFundShares;



IF OBJECT_ID('GroupReceivingBanks', 'U') IS NOT NULL DELETE FROM GroupReceivingBanks;



IF OBJECT_ID('FundTransactions', 'U') IS NOT NULL DELETE FROM FundTransactions;



IF OBJECT_ID('FundRequestRecipients', 'U') IS NOT NULL DELETE FROM FundRequestRecipients;



IF OBJECT_ID('FundRequests', 'U') IS NOT NULL DELETE FROM FundRequests;



IF OBJECT_ID('GroupRefunds', 'U') IS NOT NULL DELETE FROM GroupRefunds;



IF OBJECT_ID('GroupDeletionRefunds', 'U') IS NOT NULL DELETE FROM GroupDeletionRefunds;



IF OBJECT_ID('GroupDeletionVotes', 'U') IS NOT NULL DELETE FROM GroupDeletionVotes;



IF OBJECT_ID('GroupDeletionRequests', 'U') IS NOT NULL DELETE FROM GroupDeletionRequests;



IF OBJECT_ID('GroupExpenseShareAllocations', 'U') IS NOT NULL DELETE FROM GroupExpenseShareAllocations;



IF OBJECT_ID('GroupExpenses', 'U') IS NOT NULL DELETE FROM GroupExpenses;



IF OBJECT_ID('GroupFundRoundMembers', 'U') IS NOT NULL DELETE FROM GroupFundRoundMembers;



IF OBJECT_ID('GroupFundRounds', 'U') IS NOT NULL DELETE FROM GroupFundRounds;



IF OBJECT_ID('GroupFundRequestRecipients', 'U') IS NOT NULL DELETE FROM GroupFundRequestRecipients;



IF OBJECT_ID('GroupFundRequests', 'U') IS NOT NULL DELETE FROM GroupFundRequests;



IF OBJECT_ID('GroupContributions', 'U') IS NOT NULL DELETE FROM GroupContributions;



IF OBJECT_ID('SharedFunds', 'U') IS NOT NULL DELETE FROM SharedFunds;



IF OBJECT_ID('GroupInvites', 'U') IS NOT NULL DELETE FROM GroupInvites;



IF OBJECT_ID('GroupActivities', 'U') IS NOT NULL DELETE FROM GroupActivities;



IF OBJECT_ID('GroupMembers', 'U') IS NOT NULL DELETE FROM GroupMembers;



IF OBJECT_ID('Groups', 'U') IS NOT NULL DELETE FROM Groups;







-- Social / Friends



IF OBJECT_ID('Friendships', 'U') IS NOT NULL DELETE FROM Friendships;



IF OBJECT_ID('FriendRequests', 'U') IS NOT NULL DELETE FROM FriendRequests;







-- Planning & Recurring



IF OBJECT_ID('RecurringOccurrences', 'U') IS NOT NULL DELETE FROM RecurringOccurrences;



IF OBJECT_ID('RecurringTransactions', 'U') IS NOT NULL DELETE FROM RecurringTransactions;



IF OBJECT_ID('TransactionRules', 'U') IS NOT NULL DELETE FROM TransactionRules;







-- Support & Disputes



IF OBJECT_ID('SupportCaseMessages', 'U') IS NOT NULL DELETE FROM SupportCaseMessages;



IF OBJECT_ID('CaseHistories', 'U') IS NOT NULL DELETE FROM CaseHistories;



IF OBJECT_ID('CaseAttachments', 'U') IS NOT NULL DELETE FROM CaseAttachments;



IF OBJECT_ID('SupportCases', 'U') IS NOT NULL DELETE FROM SupportCases;







-- AI & Attachments



IF OBJECT_ID('AiActionProposals', 'U') IS NOT NULL DELETE FROM AiActionProposals;



IF OBJECT_ID('AiAttachments', 'U') IS NOT NULL DELETE FROM AiAttachments;



IF OBJECT_ID('AiMessages', 'U') IS NOT NULL DELETE FROM AiMessages;



IF OBJECT_ID('AiConversations', 'U') IS NOT NULL DELETE FROM AiConversations;



IF OBJECT_ID('EvidenceAttachments', 'U') IS NOT NULL DELETE FROM EvidenceAttachments;







-- Open Banking



IF OBJECT_ID('WalletTopUps', 'U') IS NOT NULL DELETE FROM WalletTopUps;

IF OBJECT_ID('BankLinkSessions', 'U') IS NOT NULL DELETE FROM BankLinkSessions;

IF OBJECT_ID('BankTransactions', 'U') IS NOT NULL DELETE FROM BankTransactions;



IF OBJECT_ID('BankConnections', 'U') IS NOT NULL DELETE FROM BankConnections;







-- Security tokens & sessions



IF OBJECT_ID('SecurityPinResetTokens', 'U') IS NOT NULL DELETE FROM SecurityPinResetTokens;



IF OBJECT_ID('EmailVerificationTokens', 'U') IS NOT NULL DELETE FROM EmailVerificationTokens;



IF OBJECT_ID('RefreshTokens', 'U') IS NOT NULL DELETE FROM RefreshTokens;



IF OBJECT_ID('UserSessions', 'U') IS NOT NULL DELETE FROM UserSessions;

INSERT INTO UserSessions (Id, UserId, DeviceName, DeviceIdentifier, IpAddress, UserAgent, LastSeenAt, RevokedAt, CreatedAt, UpdatedAt)
VALUES 
    ('96F998FE-81F8-432F-B80E-CC0B380AF034', 'E2E00000-0000-0000-0000-000000000001', 'TestRunner', 'device-fixture-user', '127.0.0.1', 'Newman', GETUTCDATE(), NULL, GETUTCDATE(), GETUTCDATE()),
    ('D4B0BFE0-0BA7-4F6F-A1FB-F305D8F60859', 'E2E00000-0000-0000-0000-00000000000D', 'TestRunner', 'device-fixture-member', '127.0.0.1', 'Newman', GETUTCDATE(), NULL, GETUTCDATE(), GETUTCDATE());







-- Clear Wallets and Accounts for fixture users to reset fresh



IF OBJECT_ID('FinancialTransactions', 'U') IS NOT NULL DELETE FROM FinancialTransactions WHERE OwnerUserId IN ('E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000002');



IF OBJECT_ID('LedgerEntries', 'U') IS NOT NULL DELETE FROM LedgerEntries WHERE LedgerAccountId IN ('E2E00000-0000-0000-0000-000000000004', 'E2E00000-0000-0000-0000-000000000024', 'E2E00000-0000-0000-0000-000000000014', 'E2E00000-0000-0000-0000-00000000006C', 'E2E00000-0000-0000-0000-00000000007C', 'E2E00000-0000-0000-0000-00000000009B', 'E2E00000-0000-0000-0000-00000000008E', 'E2E00000-0000-0000-0000-00000000002A', 'E2E00000-0000-0000-0000-00000000003A');



IF OBJECT_ID('LedgerAccounts', 'U') IS NOT NULL DELETE FROM LedgerAccounts WHERE Id IN ('E2E00000-0000-0000-0000-000000000004', 'E2E00000-0000-0000-0000-000000000024', 'E2E00000-0000-0000-0000-000000000014', 'E2E00000-0000-0000-0000-00000000006C', 'E2E00000-0000-0000-0000-00000000007C', 'E2E00000-0000-0000-0000-00000000009B', 'E2E00000-0000-0000-0000-00000000008E', 'E2E00000-0000-0000-0000-00000000002A', 'E2E00000-0000-0000-0000-00000000003A');



IF OBJECT_ID('Wallets', 'U') IS NOT NULL DELETE FROM Wallets WHERE Id IN ('E2E00000-0000-0000-0000-000000000003', 'E2E00000-0000-0000-0000-000000000023', 'E2E00000-0000-0000-0000-000000000013');



"@



$cleanupCmd.ExecuteNonQuery() | Out-Null



Write-Host "Cleanup completed."







# 1. Seed or Update Fixture Users



Write-Host "Seeding Fixture Users..."



$pwHash = 'pbkdf2-sha256$210000$yVVJh0+FGoU7h0YbmInbRA==$lzsHNuX72jMcjTwMkbtNrCpwBo0VN31/kCFzZd9WgC4='



$pinHash = 'pin-pbkdf2-sha256$310000$fej+FEzmEpDX+x2Z2B5Ekw==$MB31foMUnHYMieqckeRA994xtRPTf74QnEHy0Hn2RP4='







$uCmd = $conn.CreateCommand()



$uCmd.Parameters.AddWithValue("@pwHash", $pwHash) | Out-Null



$uCmd.Parameters.AddWithValue("@pinHash", $pinHash) | Out-Null



$uCmd.CommandText = @"



IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = 'E2E00000-0000-0000-0000-000000000001')



BEGIN



    INSERT INTO Users (Id, SpendId, NormalizedSpendId, Email, NormalizedEmail, FullName, Phone, PasswordHash, SecurityPinHash, EmailVerifiedAt, Status, SystemRole, PreferredLanguage, PreferredTheme, DefaultCurrency, MustChangePassword, PasswordFailedAttempts, SecurityPinFailedAttempts, EngagementEmailOptIn, CreatedAt, UpdatedAt)



    VALUES (



        'E2E00000-0000-0000-0000-000000000001',



        'e2e_fixture_user',



        'E2E_FIXTURE_USER',



        'fixture_user@spendwise.test',



        'FIXTURE_USER@SPENDWISE.TEST',



        'Fixture User',



        '0900000001',



        @pwHash,



        @pinHash,



        GETUTCDATE(),



        'Active',



        'User',



        'vi',



        'light',



        'VND',



        0,



        0,



        0,



        1,



        GETUTCDATE(),



        GETUTCDATE()



    );



END



ELSE



BEGIN



    UPDATE Users SET 



        PasswordHash = @pwHash, 



        SecurityPinHash = @pinHash, 



        PasswordFailedAttempts = 0, 



        SecurityPinFailedAttempts = 0, 



        SecurityPinLockoutUntil = NULL, 



        PasswordLockoutUntil = NULL, 



        Status = 'Active', 



        EmailVerifiedAt = ISNULL(EmailVerifiedAt, GETUTCDATE()) 



    WHERE Id = 'E2E00000-0000-0000-0000-000000000001';



END







IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = 'E2E00000-0000-0000-0000-00000000000D')



BEGIN



    INSERT INTO Users (Id, SpendId, NormalizedSpendId, Email, NormalizedEmail, FullName, Phone, PasswordHash, SecurityPinHash, EmailVerifiedAt, Status, SystemRole, PreferredLanguage, PreferredTheme, DefaultCurrency, MustChangePassword, PasswordFailedAttempts, SecurityPinFailedAttempts, EngagementEmailOptIn, CreatedAt, UpdatedAt)



    VALUES (



        'E2E00000-0000-0000-0000-00000000000D',



        'e2e_fixture_member',



        'E2E_FIXTURE_MEMBER',



        'fixture_member@spendwise.test',



        'FIXTURE_MEMBER@SPENDWISE.TEST',



        'Fixture Member',



        '0900000002',



        @pwHash,



        @pinHash,



        GETUTCDATE(),



        'Active',



        'User',



        'vi',



        'light',



        'VND',



        0,



        0,



        0,



        1,



        GETUTCDATE(),



        GETUTCDATE()



    );



END



ELSE



BEGIN



    UPDATE Users SET 



        PasswordHash = @pwHash, 



        SecurityPinHash = @pinHash, 



        PasswordFailedAttempts = 0, 



        SecurityPinFailedAttempts = 0, 



        SecurityPinLockoutUntil = NULL, 



        PasswordLockoutUntil = NULL, 



        Status = 'Active', 



        EmailVerifiedAt = ISNULL(EmailVerifiedAt, GETUTCDATE()) 



    WHERE Id = 'E2E00000-0000-0000-0000-00000000000D';



END







IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = 'E2E00000-0000-0000-0000-000000000002')



BEGIN



    INSERT INTO Users (Id, SpendId, NormalizedSpendId, Email, NormalizedEmail, FullName, Phone, PasswordHash, SecurityPinHash, EmailVerifiedAt, Status, SystemRole, PreferredLanguage, PreferredTheme, DefaultCurrency, MustChangePassword, PasswordFailedAttempts, SecurityPinFailedAttempts, EngagementEmailOptIn, CreatedAt, UpdatedAt)



    VALUES (



        'E2E00000-0000-0000-0000-000000000002',



        'e2e_fixture_peer',



        'E2E_FIXTURE_PEER',



        'fixture_peer@spendwise.test',



        'FIXTURE_PEER@SPENDWISE.TEST',



        'Fixture Peer',



        '0900000003',



        @pwHash,



        @pinHash,



        GETUTCDATE(),



        'Active',



        'User',



        'vi',



        'light',



        'VND',



        0,



        0,



        0,



        1,



        GETUTCDATE(),



        GETUTCDATE()



    );



END



ELSE



BEGIN



    UPDATE Users SET 



        PasswordHash = @pwHash, 



        SecurityPinHash = @pinHash, 



        PasswordFailedAttempts = 0, 



        SecurityPinFailedAttempts = 0, 



        SecurityPinLockoutUntil = NULL, 



        PasswordLockoutUntil = NULL, 



        Status = 'Active', 



        EmailVerifiedAt = ISNULL(EmailVerifiedAt, GETUTCDATE()) 



    WHERE Id = 'E2E00000-0000-0000-0000-000000000002';



END



"@



$uCmd.ExecuteNonQuery() | Out-Null







# 2. Seed Wallets and BankConnection and BankTransaction



Write-Host "Seeding Wallets, BankConnections, BankTransactions..."



$wCmd = $conn.CreateCommand()



$wCmd.CommandText = @"



SET QUOTED_IDENTIFIER ON;



SET ANSI_NULLS ON;



INSERT INTO Wallets (Id, OwnerUserId, Name, Type, Currency, Status, AvailableBalance, IsDefault, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000003', 'E2E00000-0000-0000-0000-000000000001', 'Default Wallet', 'Main', 'VND', 'Active', 10000000, 1, GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000023', 'E2E00000-0000-0000-0000-000000000001', 'Savings Wallet', 'Savings', 'VND', 'Active', 0, 0, GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000013', 'E2E00000-0000-0000-0000-00000000000D', 'Default Wallet', 'Main', 'VND', 'Active', 10000000, 1, GETUTCDATE(), GETUTCDATE());







INSERT INTO LedgerAccounts (Id, Code, Name, Type, Currency, Balance, WalletId, OwnerUserId, IsSystem, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000004', 'FIXTURE_USER_WALLET', 'Fixture User Account', 'Wallet', 'VND', 10000000, 'E2E00000-0000-0000-0000-000000000003', 'E2E00000-0000-0000-0000-000000000001', 0, GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000024', 'FIXTURE_USER_SAVINGS', 'Fixture User Savings', 'Wallet', 'VND', 0, 'E2E00000-0000-0000-0000-000000000023', 'E2E00000-0000-0000-0000-000000000001', 0, GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000014', 'FIXTURE_MEMBER_WALLET', 'Fixture Member Account', 'Wallet', 'VND', 10000000, 'E2E00000-0000-0000-0000-000000000013', 'E2E00000-0000-0000-0000-00000000000D', 0, GETUTCDATE(), GETUTCDATE());







INSERT INTO BankConnections (Id, UserId, Provider, ExternalAccountId, BankBrandName, AccountType, AccountHolderName, MaskedAccountNumber, Status, AccountEnvironment, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000005', 'E2E00000-0000-0000-0000-000000000001', 'SePayBankHub', 'EXT-ACC-001', 'MBBank', 'Personal', 'FIXTURE USER', '****1234', 'Active', 'TestMode', GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000015', 'E2E00000-0000-0000-0000-00000000000D', 'SePayBankHub', 'EXT-ACC-002', 'MBBank', 'Personal', 'FIXTURE MEMBER', '****5678', 'Active', 'TestMode', GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000025', 'E2E00000-0000-0000-0000-000000000001', 'SePayBankHub', 'EXT-ACC-TEST', 'MBBank', 'Personal', 'FIXTURE USER TEST', '****9999', 'Active', 'TestMode', GETUTCDATE(), GETUTCDATE());







INSERT INTO BankTransactions (Id, UserId, BankConnectionId, Provider, ExternalTransactionId, Direction, Amount, Currency, TransactionDate, Content, Source, ImportedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000006', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000005', 'SePayBankHub', 'EXT-TX-001', 'Credit', 500000, 'VND', GETUTCDATE(), 'Top up test', 'Webhook', GETUTCDATE(), GETUTCDATE(), GETUTCDATE());

UPDATE BankConnections SET Status = 'Active', UnlinkedAt = NULL WHERE Id = 'E2E00000-0000-0000-0000-000000000025';

INSERT INTO WalletTopUps (Id, UserId, BankConnectionId, WalletId, Amount, Currency, Reference, Status, ExpiresAt, CreatedAt, UpdatedAt)
VALUES ('E2E00000-0000-0000-0000-000000000050', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000005', 'E2E00000-0000-0000-0000-000000000003', 100000, 'VND', 'SWTPE2E001', 'Pending', DATEADD(hour, 2, GETUTCDATE()), GETUTCDATE(), GETUTCDATE());



"@



$wCmd.ExecuteNonQuery() | Out-Null







# 3. Seed Support Case & History & Attachment & Dispute



Write-Host "Seeding Cases and Attachments..."



$cCmd = $conn.CreateCommand()



$cCmd.CommandText = @"



-- Support Case



INSERT INTO SupportCases (Id, TicketNumber, LinkedUserId, Subject, Category, Description, Status, Priority, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000007', 'TICK-E2E-0007', 'E2E00000-0000-0000-0000-000000000001', 'Fixture Case', 'General', 'E2E Test case for attachment retrieval', 'Open', 'Normal', GETUTCDATE(), GETUTCDATE());







INSERT INTO CaseHistories (Id, CaseKind, CaseId, ActorId, ActorDisplayNameSnapshot, ActorRole, Action, Message, OldStatus, NewStatus, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000017', 'Support', 'E2E00000-0000-0000-0000-000000000007', 'E2E00000-0000-0000-0000-000000000001', 'Fixture User', 'User', 'Created', 'Case created with attachment', NULL, 'Open', GETUTCDATE(), GETUTCDATE());







INSERT INTO CaseAttachments (Id, CaseHistoryId, CaseOwnerUserId, OriginalFileName, ContentType, Length, ContentHash, Content, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000008', 'E2E00000-0000-0000-0000-000000000017', 'E2E00000-0000-0000-0000-000000000001', 'dummy.png', 'image/png', 67, 'b5bb9d8014a0f9b1d61e21e796d78dccdf1352f23cd32812f4850b878ae4944c', 0x89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000D49444154789C6360606060000000050001A7414A940000000049454E44AE426082, GETUTCDATE(), GETUTCDATE());







-- Dispute Case



INSERT INTO SupportCases (Id, TicketNumber, LinkedUserId, Subject, Category, Description, Status, Priority, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000091', 'TICK-E2E-0091', 'E2E00000-0000-0000-0000-000000000001', 'Fixture Dispute', 'TransactionDispute', 'Dispute over transaction', 'Open', 'High', GETUTCDATE(), GETUTCDATE());







INSERT INTO CaseHistories (Id, CaseKind, CaseId, ActorId, ActorDisplayNameSnapshot, ActorRole, Action, Message, OldStatus, NewStatus, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000092', 'Dispute', 'E2E00000-0000-0000-0000-000000000091', 'E2E00000-0000-0000-0000-000000000001', 'Fixture User', 'User', 'Created', 'Dispute opened', NULL, 'Open', GETUTCDATE(), GETUTCDATE());







INSERT INTO CaseAttachments (Id, CaseHistoryId, CaseOwnerUserId, OriginalFileName, ContentType, Length, ContentHash, Content, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000093', 'E2E00000-0000-0000-0000-000000000092', 'E2E00000-0000-0000-0000-000000000001', 'dispute.png', 'image/png', 67, 'b5bb9d8014a0f9b1d61e21e796d78dccdf1352f23cd32812f4850b878ae4944c', 0x89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000D49444154789C6360606060000000050001A7414A940000000049454E44AE426082, GETUTCDATE(), GETUTCDATE());



"@



$cCmd.ExecuteNonQuery() | Out-Null







# 4. Seed AI Action Proposals & PIN Reset Token (Bound to User 2)



Write-Host "Seeding AI Action Proposals and PIN Reset Token..."



$rawToken = "E2E_PIN_RESET_SECRET_TOKEN_XYZ_12345"



$sha = [System.Security.Cryptography.SHA256]::Create()



$pinTokenHash = [System.BitConverter]::ToString($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($rawToken))).Replace('-', '')







$aiCmd = $conn.CreateCommand()



$aiCmd.CommandText = @"



INSERT INTO AiAttachments (Id, OwnerUserId, OriginalFileName, ContentType, Length, ContentHash, Content, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-0000000000B1', 'E2E00000-0000-0000-0000-000000000001', 'ai_sample.txt', 'text/plain', 24, 'b712c99dc0819b9cd1a10d48a73d5c4c59523ea0b6403d2efdabb8b3c0e50893', 0x5370656E64576973652073616D706C6520636F6E74656E74, GETUTCDATE(), GETUTCDATE());







INSERT INTO EvidenceAttachments (Id, OwnerUserId, OriginalFileName, ContentType, Length, ContentHash, Content, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000085', 'E2E00000-0000-0000-0000-000000000001', 'receipt.png', 'image/png', 67, 'b5bb9d8014a0f9b1d61e21e796d78dccdf1352f23cd32812f4850b878ae4944c', 0x89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000D49444154789C6360606060000000050001A7414A940000000049454E44AE426082, GETUTCDATE(), GETUTCDATE());







INSERT INTO RecurringTransactions (Id, OwnerUserId, WalletId, CategoryId, Kind, Amount, Description, Title, Frequency, Status, IsSubscription, NextRunAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000080', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000005', 'Expense', 50000, 'Monthly Internet', 'Internet', 'Monthly', 'Active', 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO RecurringOccurrences (Id, RecurringTransactionId, ScheduledFor, Status, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000081', 'E2E00000-0000-0000-0000-000000000080', GETUTCDATE(), 'Due', GETUTCDATE(), GETUTCDATE());







INSERT INTO FriendRequests (Id, RequesterUserId, AddresseeUserId, LowerUserId, HigherUserId, Message, Status, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000071', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000002', 'Cancel test', 'Pending', GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000072', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-00000000000D', 'Reject test', 'Pending', GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000070', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-00000000000D', 'Accepted for friendship', 'Accepted', GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000079', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000002', 'Accepted for unfriend', 'Accepted', GETUTCDATE(), GETUTCDATE());







INSERT INTO Friendships (Id, LowerUserId, HigherUserId, AcceptedRequestId, AcceptedAt, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000073', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000070', GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000074', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-000000000079', GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO AiActionProposals (Id, OwnerUserId, ActionType, PayloadJson, SafeSummary, Status, OperationKey, ExpiresAt, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000009', 'E2E00000-0000-0000-0000-000000000001', 'group_create', '{"name":"AI Test Group Execute","description":"AI created group","currency":"VND","type":"Friends"}', 'Create friends group via AI proposal', 'Pending', 'e2e-op-key-exec', DATEADD(day, 2, GETUTCDATE()), GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-00000000000A', 'E2E00000-0000-0000-0000-000000000001', 'group_create', '{"name":"AI Test Group Cancel","description":"AI cancel group","currency":"VND","type":"Friends"}', 'Create friends group to cancel', 'Pending', 'e2e-op-key-cancel', DATEADD(day, 2, GETUTCDATE()), GETUTCDATE(), GETUTCDATE());







-- PIN reset token bound to User 2 so UserFixture token is NOT revoked!



INSERT INTO SecurityPinResetTokens (Id, UserId, Purpose, TokenHash, ExpiresAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000000B', 'E2E00000-0000-0000-0000-000000000002', 'SelfService', '$pinTokenHash', DATEADD(day, 2, GETUTCDATE()), GETUTCDATE(), GETUTCDATE());



"@



$aiCmd.ExecuteNonQuery() | Out-Null







# 5. Seed V1 Funds & Governance Fixtures



Write-Host "Seeding V1 Funds & Governance Fixtures..."



$fCmd = $conn.CreateCommand()



$fCmd.CommandText = @"



-- FUND 1 (For Election Registration & Candidate Register)



INSERT INTO V1Funds (Id, Name, Code, Kind, FounderUserId, OwnerUserId, OwnershipRevision, TargetVnd, ActivityClosedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000000C', 'E2E Fixture Fixed Fund 1', 'FIX01', 'Fixed', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000001', 1, 1000000, DATEADD(day, -10, GETUTCDATE()), GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundMemberships (Id, FundId, UserId, Seat, CanSpend, JoinedAt, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000021', 'E2E00000-0000-0000-0000-00000000000C', 'E2E00000-0000-0000-0000-000000000001', 1, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000022', 'E2E00000-0000-0000-0000-00000000000C', 'E2E00000-0000-0000-0000-00000000000D', 2, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO LedgerAccounts (Id, Code, Name, Type, Currency, Balance, OwnerUserId, IsSystem, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000006C', 'FUND_1_TREASURY', 'Fund 1 Treasury', 'SharedFund', 'VND', 1000000, 'E2E00000-0000-0000-0000-000000000001', 0, GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundTreasuries (Id, FundId, LedgerAccountId, Revision, TargetVnd, MembershipSignature, CreatedAt, UpdatedAt)



VALUES (NEWID(), 'E2E00000-0000-0000-0000-00000000000C', 'E2E00000-0000-0000-0000-00000000006C', 1, 1000000, 'sig-fund-1', GETUTCDATE(), GETUTCDATE());







INSERT INTO V1Elections (Id, FundId, IncumbentUserId, OpenedByUserId, OwnershipRevision, Phase, WinnerUserId, PhaseStartedAt, PolicyJson, InactivityEvidenceId, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000000E', 'E2E00000-0000-0000-0000-00000000000C', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-00000000000D', 1, 0, NULL, GETUTCDATE(), '{"Version":"V1","RegistrationSeconds":86400,"NominationSeconds":60,"RunoffSeconds":60,"ConfirmationSeconds":60,"AcceptanceSeconds":60,"AllowContributions":false}', 'owner-response:1', GETUTCDATE(), GETUTCDATE());







INSERT INTO V1ElectionVoters (Id, ElectionId, UserId, MembershipId, CreatedAt, UpdatedAt)



VALUES 



    (NEWID(), 'E2E00000-0000-0000-0000-00000000000E', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000021', GETUTCDATE(), GETUTCDATE()),



    (NEWID(), 'E2E00000-0000-0000-0000-00000000000E', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000022', GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundRequests (Id, FundId, RequestedByUserId, RecipientUserId, RequestedByMembershipId, OwnershipRevision, Kind, AmountVnd, Purpose, IdempotencyKey, State, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000095', 'E2E00000-0000-0000-0000-00000000000C', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000021', 1, 'Expense', 100000, 'Large office expense', 'req-hold-001', 'Pending24h', GETUTCDATE(), GETUTCDATE());











-- FUND 2 (For Escalate Owner Response & Election Accept)



DECLARE @closedDate2 DATETIME2 = DATEADD(day, -10, GETUTCDATE());



INSERT INTO V1Funds (Id, Name, Code, Kind, FounderUserId, OwnerUserId, OwnershipRevision, TargetVnd, ActivityClosedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000001C', 'E2E Fixture Fixed Fund 2', 'FIX02', 'Fixed', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000001', 1, 1000000, @closedDate2, GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundMemberships (Id, FundId, UserId, Seat, CanSpend, JoinedAt, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000031', 'E2E00000-0000-0000-0000-00000000001C', 'E2E00000-0000-0000-0000-000000000001', 1, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000032', 'E2E00000-0000-0000-0000-00000000001C', 'E2E00000-0000-0000-0000-00000000000D', 2, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO LedgerAccounts (Id, Code, Name, Type, Currency, Balance, OwnerUserId, IsSystem, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000002A', 'FUND_2_TREASURY', 'Fund 2 Treasury', 'SharedFund', 'VND', 0, 'E2E00000-0000-0000-0000-000000000001', 0, GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundTreasuries (Id, FundId, LedgerAccountId, Revision, TargetVnd, MembershipSignature, CreatedAt, UpdatedAt)



VALUES (NEWID(), 'E2E00000-0000-0000-0000-00000000001C', 'E2E00000-0000-0000-0000-00000000002A', 1, 1000000, 'sig-fund-2', GETUTCDATE(), GETUTCDATE());







INSERT INTO V1Elections (Id, FundId, IncumbentUserId, OpenedByUserId, OwnershipRevision, Phase, WinnerUserId, PhaseStartedAt, PolicyJson, InactivityEvidenceId, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000000F', 'E2E00000-0000-0000-0000-00000000001C', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-00000000000D', 1, 4, 'E2E00000-0000-0000-0000-00000000000D', GETUTCDATE(), '{"Version":"V1","RegistrationSeconds":86400,"NominationSeconds":86400,"RunoffSeconds":86400,"ConfirmationSeconds":86400,"AcceptanceSeconds":86400,"AllowContributions":false}', 'owner-response:2', GETUTCDATE(), GETUTCDATE());







INSERT INTO V1ElectionVoters (Id, ElectionId, UserId, MembershipId, CreatedAt, UpdatedAt)



VALUES (NEWID(), 'E2E00000-0000-0000-0000-00000000000F', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000032', GETUTCDATE(), GETUTCDATE());











-- FUND 3 (For Election Reject)



DECLARE @closedDate3 DATETIME2 = DATEADD(day, -10, GETUTCDATE());



INSERT INTO V1Funds (Id, Name, Code, Kind, FounderUserId, OwnerUserId, OwnershipRevision, TargetVnd, ActivityClosedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000002C', 'E2E Fixture Fixed Fund 3', 'FIX03', 'Fixed', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000001', 1, 1000000, @closedDate3, GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundMemberships (Id, FundId, UserId, Seat, CanSpend, JoinedAt, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000041', 'E2E00000-0000-0000-0000-00000000002C', 'E2E00000-0000-0000-0000-000000000001', 1, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000042', 'E2E00000-0000-0000-0000-00000000002C', 'E2E00000-0000-0000-0000-00000000000D', 2, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO LedgerAccounts (Id, Code, Name, Type, Currency, Balance, OwnerUserId, IsSystem, CreatedAt, UpdatedAt)
VALUES ('E2E00000-0000-0000-0000-00000000003A', 'FUND_3_TREASURY', 'Fund 3 Treasury', 'SharedFund', 'VND', 0, 'E2E00000-0000-0000-0000-000000000001', 0, GETUTCDATE(), GETUTCDATE());

INSERT INTO V1FundTreasuries (Id, FundId, LedgerAccountId, Revision, TargetVnd, MembershipSignature, CreatedAt, UpdatedAt)
VALUES (NEWID(), 'E2E00000-0000-0000-0000-00000000002C', 'E2E00000-0000-0000-0000-00000000003A', 1, 1000000, 'sig-fund-3', GETUTCDATE(), GETUTCDATE());

INSERT INTO V1Elections (Id, FundId, IncumbentUserId, OpenedByUserId, OwnershipRevision, Phase, WinnerUserId, PhaseStartedAt, PolicyJson, InactivityEvidenceId, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000010', 'E2E00000-0000-0000-0000-00000000002C', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-00000000000D', 1, 4, 'E2E00000-0000-0000-0000-00000000000D', GETUTCDATE(), '{"Version":"V1","RegistrationSeconds":86400,"NominationSeconds":86400,"RunoffSeconds":86400,"ConfirmationSeconds":86400,"AcceptanceSeconds":86400,"AllowContributions":false}', 'owner-response:3', GETUTCDATE(), GETUTCDATE());







INSERT INTO V1ElectionVoters (Id, ElectionId, UserId, MembershipId, CreatedAt, UpdatedAt)



VALUES (NEWID(), 'E2E00000-0000-0000-0000-000000000010', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000042', GETUTCDATE(), GETUTCDATE());











-- FUND 4 (For Activity Proposals Settlement - NO ACTIVE ELECTION!)



DECLARE @closedDate4 DATETIME2 = DATEADD(day, -5, GETUTCDATE());



INSERT INTO V1Funds (Id, Name, Code, Kind, FounderUserId, OwnerUserId, OwnershipRevision, TargetVnd, ActivityClosedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000003C', 'E2E Fixture Fixed Fund 4', 'FIX04', 'Fixed', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000001', 1, 1000000, @closedDate4, GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundMemberships (Id, FundId, UserId, Seat, CanSpend, JoinedAt, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000051', 'E2E00000-0000-0000-0000-00000000003C', 'E2E00000-0000-0000-0000-000000000001', 1, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO LedgerAccounts (Id, Code, Name, Type, Currency, Balance, OwnerUserId, IsSystem, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000007C', 'FUND_4_TREASURY', 'Fund 4 Treasury', 'SharedFund', 'VND', 0, 'E2E00000-0000-0000-0000-000000000001', 0, GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundTreasuries (Id, FundId, LedgerAccountId, Revision, TargetVnd, MembershipSignature, CreatedAt, UpdatedAt)



VALUES (NEWID(), 'E2E00000-0000-0000-0000-00000000003C', 'E2E00000-0000-0000-0000-00000000007C', 1, 1000000, 'sig-fund-4', GETUTCDATE(), GETUTCDATE());







-- Proposal 0011: For Settle Activity Proposal



INSERT INTO V1ActivityProposals (Id, FundId, ProposedByUserId, OwnershipRevision, TargetVnd, State, IdempotencyKey, OpenedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000011', 'E2E00000-0000-0000-0000-00000000003C', 'E2E00000-0000-0000-0000-000000000001', 1, 500000, 'ApprovedAwaitingSettlement', 'prop-e2e-settle-key', DATEADD(day, -2, GETUTCDATE()), GETUTCDATE(), GETUTCDATE());







INSERT INTO V1ActivityVoters (Id, ProposalId, MembershipId, UserId, Support, CreatedAt, UpdatedAt)



VALUES 



    (NEWID(), 'E2E00000-0000-0000-0000-000000000011', 'E2E00000-0000-0000-0000-000000000051', 'E2E00000-0000-0000-0000-000000000001', 1, GETUTCDATE(), GETUTCDATE());











-- FUND 9 (For Activity Proposal Voting - Separate fund to honor IX_V1ActivityProposals_FundId)



INSERT INTO V1Funds (Id, Name, Code, Kind, FounderUserId, OwnerUserId, OwnershipRevision, TargetVnd, ActivityClosedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000008C', 'E2E Fixture Fixed Fund 9', 'FIX09', 'Fixed', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000001', 1, 1000000, NULL, GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundMemberships (Id, FundId, UserId, Seat, CanSpend, JoinedAt, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-0000000000A5', 'E2E00000-0000-0000-0000-00000000008C', 'E2E00000-0000-0000-0000-000000000001', 1, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-0000000000A6', 'E2E00000-0000-0000-0000-00000000008C', 'E2E00000-0000-0000-0000-00000000000D', 2, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO V1ActivityProposals (Id, FundId, ProposedByUserId, OwnershipRevision, TargetVnd, State, IdempotencyKey, OpenedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000012', 'E2E00000-0000-0000-0000-00000000008C', 'E2E00000-0000-0000-0000-000000000001', 1, 800000, 'Voting', 'prop-e2e-vote-key', DATEADD(minute, -10, GETUTCDATE()), GETUTCDATE(), GETUTCDATE());







INSERT INTO V1ActivityVoters (Id, ProposalId, MembershipId, UserId, Support, CreatedAt, UpdatedAt)



VALUES 



    (NEWID(), 'E2E00000-0000-0000-0000-000000000012', 'E2E00000-0000-0000-0000-0000000000A5', 'E2E00000-0000-0000-0000-000000000001', NULL, GETUTCDATE(), GETUTCDATE()),



    (NEWID(), 'E2E00000-0000-0000-0000-000000000012', 'E2E00000-0000-0000-0000-0000000000A6', 'E2E00000-0000-0000-0000-00000000000D', NULL, GETUTCDATE(), GETUTCDATE());











-- FUND 5 (For Open Election - NO ACTIVE ELECTION!)



DECLARE @closedDate5 DATETIME2 = DATEADD(day, -10, GETUTCDATE());



INSERT INTO V1Funds (Id, Name, Code, Kind, FounderUserId, OwnerUserId, OwnershipRevision, TargetVnd, ActivityClosedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000004C', 'E2E Fixture Fixed Fund 5', 'FIX05', 'Fixed', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-000000000002', 1, 1000000, @closedDate5, GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundMemberships (Id, FundId, UserId, Seat, CanSpend, JoinedAt, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000061', 'E2E00000-0000-0000-0000-00000000004C', 'E2E00000-0000-0000-0000-000000000002', 1, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000062', 'E2E00000-0000-0000-0000-00000000004C', 'E2E00000-0000-0000-0000-00000000000D', 2, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







IF NOT EXISTS (SELECT 1 FROM LedgerTransactions WHERE Id = 'DA836139-50F4-4B78-B1A9-0A341B91A44D')



    INSERT INTO LedgerTransactions (Id, Reference, BusinessType, BusinessId, Description, Status, CompletedAt, CreatedAt, UpdatedAt)



    VALUES ('DA836139-50F4-4B78-B1A9-0A341B91A44D', 'REF-SEED-03', 'Contribution', 'E2E00000-0000-0000-0000-00000000004C', 'Seed contribution', 'Completed', GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundContributions (Id, FundId, UserId, MembershipId, WalletId, AmountVnd, IdempotencyKey, Purpose, LedgerTransactionId, CreatedAt)



VALUES 
    ('E2E00000-0000-0000-0000-000000000063', 'E2E00000-0000-0000-0000-00000000004C', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000062', 'E2E00000-0000-0000-0000-000000000013', 50000, 'contrib-member-fund5', 'Initial', 'DA836139-50F4-4B78-B1A9-0A341B91A44D', GETUTCDATE()),
    ('E2E00000-0000-0000-0000-000000000064', 'E2E00000-0000-0000-0000-00000000000C', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000022', 'E2E00000-0000-0000-0000-000000000013', 50000, 'contrib-member-fund1', 'Initial', 'DA836139-50F4-4B78-B1A9-0A341B91A44D', GETUTCDATE());







INSERT INTO V1Elections (Id, FundId, IncumbentUserId, OpenedByUserId, OwnershipRevision, Phase, WinnerUserId, PhaseStartedAt, PolicyJson, InactivityEvidenceId, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000002E', 'E2E00000-0000-0000-0000-00000000004C', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-00000000000D', 1, 0, NULL, DATEADD(day, -2, GETUTCDATE()), '{"Version":"V1","RegistrationSeconds":60,"NominationSeconds":60,"RunoffSeconds":60,"ConfirmationSeconds":60,"AcceptanceSeconds":60,"AllowContributions":false}', 'owner-response:5', GETUTCDATE(), GETUTCDATE());







INSERT INTO V1ElectionVoters (Id, ElectionId, UserId, MembershipId, CreatedAt, UpdatedAt)



VALUES 



    (NEWID(), 'E2E00000-0000-0000-0000-00000000002E', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-000000000061', GETUTCDATE(), GETUTCDATE()),



    (NEWID(), 'E2E00000-0000-0000-0000-00000000002E', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000062', GETUTCDATE(), GETUTCDATE());















-- FUND 6 (For Cast Ballot - Phase 1 Nomination)



INSERT INTO V1Funds (Id, Name, Code, Kind, FounderUserId, OwnerUserId, OwnershipRevision, TargetVnd, ActivityClosedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000005C', 'E2E Fixture Fixed Fund 6', 'FIX06', 'Fixed', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-000000000002', 1, 1000000, DATEADD(day, -10, GETUTCDATE()), GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundMemberships (Id, FundId, UserId, Seat, CanSpend, JoinedAt, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000071', 'E2E00000-0000-0000-0000-00000000005C', 'E2E00000-0000-0000-0000-000000000002', 1, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000072', 'E2E00000-0000-0000-0000-00000000005C', 'E2E00000-0000-0000-0000-00000000000D', 2, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO V1Elections (Id, FundId, IncumbentUserId, OpenedByUserId, OwnershipRevision, Phase, WinnerUserId, PhaseStartedAt, PolicyJson, InactivityEvidenceId, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000001E', 'E2E00000-0000-0000-0000-00000000005C', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-00000000000D', 1, 1, NULL, GETUTCDATE(), '{"Version":"V1","RegistrationSeconds":86400,"NominationSeconds":86400,"RunoffSeconds":86400,"ConfirmationSeconds":86400,"AcceptanceSeconds":86400,"AllowContributions":false}', 'owner-response:6', GETUTCDATE(), GETUTCDATE());







INSERT INTO V1ElectionVoters (Id, ElectionId, UserId, MembershipId, CreatedAt, UpdatedAt)



VALUES 



    (NEWID(), 'E2E00000-0000-0000-0000-00000000001E', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-000000000071', GETUTCDATE(), GETUTCDATE()),



    (NEWID(), 'E2E00000-0000-0000-0000-00000000001E', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000072', GETUTCDATE(), GETUTCDATE());







INSERT INTO V1ElectionCandidates (Id, ElectionId, UserId, InRunoff, CreatedAt, UpdatedAt)



VALUES (NEWID(), 'E2E00000-0000-0000-0000-00000000001E', 'E2E00000-0000-0000-0000-00000000000D', 0, GETUTCDATE(), GETUTCDATE());











-- FUND 7 (For Accept Ownership Offer - NO ACTIVE ELECTION!)



INSERT INTO V1Funds (Id, Name, Code, Kind, FounderUserId, OwnerUserId, OwnershipRevision, TargetVnd, ActivityClosedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000006C', 'E2E Fixture Fixed Fund 7', 'FIX07', 'Fixed', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-000000000002', 1, 1000000, NULL, GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundMemberships (Id, FundId, UserId, Seat, CanSpend, JoinedAt, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000081', 'E2E00000-0000-0000-0000-00000000006C', 'E2E00000-0000-0000-0000-000000000002', 1, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-000000000082', 'E2E00000-0000-0000-0000-00000000006C', 'E2E00000-0000-0000-0000-00000000000D', 2, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO V1OwnershipOffers (Id, FundId, FromUserId, ToUserId, ExpectedOwnershipRevision, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000099', 'E2E00000-0000-0000-0000-00000000006C', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-00000000000D', 1, GETUTCDATE(), GETUTCDATE());











-- FUND 8 (For Review Invitation Proposed)



INSERT INTO V1Funds (Id, Name, Code, Kind, FounderUserId, OwnerUserId, OwnershipRevision, TargetVnd, ActivityClosedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000007C', 'E2E Fixture Fixed Fund 8', 'FIX08', 'Fixed', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000001', 1, 1000000, NULL, GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundMemberships (Id, FundId, UserId, Seat, CanSpend, JoinedAt, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-000000000091', 'E2E00000-0000-0000-0000-00000000007C', 'E2E00000-0000-0000-0000-000000000001', 1, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundInvitations (Id, FundId, InvitedUserId, InvitedByUserId, Status, ExpiresAt, CreatedAt, UpdatedAt)



VALUES 



    ('E2E00000-0000-0000-0000-0000000000A1', 'E2E00000-0000-0000-0000-00000000007C', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000001', 'Proposed', DATEADD(day, 7, GETUTCDATE()), GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-0000000000A2', 'E2E00000-0000-0000-0000-00000000000C', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000001', 'Pending', DATEADD(day, 7, GETUTCDATE()), GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-0000000000A3', 'E2E00000-0000-0000-0000-00000000001C', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000001', 'Pending', DATEADD(day, 7, GETUTCDATE()), GETUTCDATE(), GETUTCDATE()),



    ('E2E00000-0000-0000-0000-0000000000A4', 'E2E00000-0000-0000-0000-00000000002C', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000001', 'Pending', DATEADD(day, 7, GETUTCDATE()), GETUTCDATE(), GETUTCDATE());







-- EXTRA FIXTURES FOR 100% HAPPY PATH READINESS







-- Fund 10: Dedicated for Propose Activity Test



INSERT INTO V1Funds (Id, Name, Code, Kind, FounderUserId, OwnerUserId, OwnershipRevision, TargetVnd, ActivityClosedAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000009C', 'E2E Fixture Fixed Fund 10', 'FIX10', 'Fixed', 'E2E00000-0000-0000-0000-000000000001', 'E2E00000-0000-0000-0000-000000000001', 1, 1000000, DATEADD(day, -5, GETUTCDATE()), GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundMemberships (Id, FundId, UserId, Seat, CanSpend, JoinedAt, CreatedAt, UpdatedAt)

VALUES 

    ('E2E00000-0000-0000-0000-0000000000B1', 'E2E00000-0000-0000-0000-00000000009C', 'E2E00000-0000-0000-0000-000000000001', 1, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),

    ('E2E00000-0000-0000-0000-0000000000B2', 'E2E00000-0000-0000-0000-00000000009C', 'E2E00000-0000-0000-0000-000000000002', 2, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());



INSERT INTO LedgerAccounts (Id, Code, Name, Type, Currency, Balance, OwnerUserId, IsSystem, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000009B', 'FUND_10_TREASURY', 'Fund 10 Treasury', 'SharedFund', 'VND', 0, 'E2E00000-0000-0000-0000-000000000001', 0, GETUTCDATE(), GETUTCDATE());







INSERT INTO V1FundTreasuries (Id, FundId, LedgerAccountId, Revision, TargetVnd, MembershipSignature, CreatedAt, UpdatedAt)



VALUES (NEWID(), 'E2E00000-0000-0000-0000-00000000009C', 'E2E00000-0000-0000-0000-00000000009B', 1, 1000000, 'sig-fund-10', GETUTCDATE(), GETUTCDATE());


-- Fund 11: Dedicated for B6-15 Reject Elected Ownership Test (Permanent Active Fund, No Interference)
INSERT INTO V1Funds (Id, Name, Code, Kind, FounderUserId, OwnerUserId, OwnershipRevision, TargetVnd, ActivityClosedAt, CreatedAt, UpdatedAt)
VALUES ('E2E00000-0000-0000-0000-0000000000AC', 'E2E Fixture Fixed Fund 11', 'FIX11', 'Fixed', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-000000000002', 1, 1000000, NULL, GETUTCDATE(), GETUTCDATE());

INSERT INTO V1FundMemberships (Id, FundId, UserId, Seat, CanSpend, JoinedAt, CreatedAt, UpdatedAt)
VALUES 
    ('E2E00000-0000-0000-0000-0000000000C1', 'E2E00000-0000-0000-0000-0000000000AC', 'E2E00000-0000-0000-0000-000000000002', 1, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),
    ('E2E00000-0000-0000-0000-0000000000C2', 'E2E00000-0000-0000-0000-0000000000AC', 'E2E00000-0000-0000-0000-00000000000D', 2, 1, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());

INSERT INTO LedgerAccounts (Id, Code, Name, Type, Currency, Balance, OwnerUserId, IsSystem, CreatedAt, UpdatedAt)
VALUES ('E2E00000-0000-0000-0000-0000000000CB', 'FUND_11_TREASURY', 'Fund 11 Treasury', 'SharedFund', 'VND', 0, 'E2E00000-0000-0000-0000-000000000002', 0, GETUTCDATE(), GETUTCDATE());

INSERT INTO V1FundTreasuries (Id, FundId, LedgerAccountId, Revision, TargetVnd, MembershipSignature, CreatedAt, UpdatedAt)
VALUES (NEWID(), 'E2E00000-0000-0000-0000-0000000000AC', 'E2E00000-0000-0000-0000-0000000000CB', 1, 1000000, 'sig-fund-11', GETUTCDATE(), GETUTCDATE());

INSERT INTO V1Elections (Id, FundId, IncumbentUserId, OpenedByUserId, OwnershipRevision, Phase, WinnerUserId, PhaseStartedAt, PolicyJson, InactivityEvidenceId, CreatedAt, UpdatedAt)
VALUES ('E2E00000-0000-0000-0000-000000000020', 'E2E00000-0000-0000-0000-0000000000AC', 'E2E00000-0000-0000-0000-000000000002', 'E2E00000-0000-0000-0000-00000000000D', 1, 4, 'E2E00000-0000-0000-0000-00000000000D', GETUTCDATE(), '{"Version":"V1","RegistrationSeconds":86400,"NominationSeconds":86400,"RunoffSeconds":86400,"ConfirmationSeconds":86400,"AcceptanceSeconds":86400,"AllowContributions":false}', 'owner-response:11', GETUTCDATE(), GETUTCDATE());

INSERT INTO V1ElectionVoters (Id, ElectionId, UserId, MembershipId, CreatedAt, UpdatedAt)
VALUES (NEWID(), 'E2E00000-0000-0000-0000-000000000020', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-0000000000C2', GETUTCDATE(), GETUTCDATE());







-- Legacy Group 1: For Reject Invite



INSERT INTO Groups (Id, Name, Type, Currency, Status, MembersCanCreateExpenses, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000087', 'Invite Reject Group', 'Friends', 'VND', 'Active', 1, GETUTCDATE(), GETUTCDATE());







INSERT INTO GroupInvites (Id, GroupId, InvitedUserId, InvitedByUserId, Token, Status, ExpiresAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000088', 'E2E00000-0000-0000-0000-000000000087', 'E2E00000-0000-0000-0000-00000000000D', 'E2E00000-0000-0000-0000-000000000001', 'tok-rej-001', 'Pending', DATEADD(day, 7, GETUTCDATE()), GETUTCDATE(), GETUTCDATE());







-- Legacy Group 2: For Remove Member & Transfer Ownership



INSERT INTO Groups (Id, Name, Type, Currency, Status, MembersCanCreateExpenses, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-000000000089', 'Ops Test Group', 'Friends', 'VND', 'Active', 1, GETUTCDATE(), GETUTCDATE());







INSERT INTO GroupMembers (Id, GroupId, UserId, Role, JoinedAt, CreatedAt, UpdatedAt)



VALUES 



    (NEWID(), 'E2E00000-0000-0000-0000-000000000089', 'E2E00000-0000-0000-0000-000000000001', 'Owner', GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),



    (NEWID(), 'E2E00000-0000-0000-0000-000000000089', 'E2E00000-0000-0000-0000-000000000002', 'Member', GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),



    (NEWID(), 'E2E00000-0000-0000-0000-000000000089', 'E2E00000-0000-0000-0000-00000000000D', 'Member', GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







-- Legacy Group 3: For Deletion-Request Get, Refund Payment, and Finalize



INSERT INTO Groups (Id, Name, Type, Currency, Status, MembersCanCreateExpenses, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000008A', 'Refund Test Group', 'Friends', 'VND', 'DeletionPending', 1, GETUTCDATE(), GETUTCDATE());







INSERT INTO GroupMembers (Id, GroupId, UserId, Role, JoinedAt, CreatedAt, UpdatedAt)



VALUES 



    (NEWID(), 'E2E00000-0000-0000-0000-00000000008A', 'E2E00000-0000-0000-0000-000000000001', 'Owner', GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),



    (NEWID(), 'E2E00000-0000-0000-0000-00000000008A', 'E2E00000-0000-0000-0000-00000000000D', 'Member', GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO LedgerAccounts (Id, Code, Name, Type, Currency, Balance, OwnerUserId, IsSystem, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000008E', 'REFUND_GRP_TREASURY', 'Refund Group Treasury', 'SharedFund', 'VND', 10000, 'E2E00000-0000-0000-0000-000000000001', 0, GETUTCDATE(), GETUTCDATE());







INSERT INTO SharedFunds (Id, GroupId, LedgerAccountId, Balance, Currency, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000008F', 'E2E00000-0000-0000-0000-00000000008A', 'E2E00000-0000-0000-0000-00000000008E', 10000, 'VND', GETUTCDATE(), GETUTCDATE());







INSERT INTO GroupFundShares (Id, GroupId, UserId, RemainingAmount, CreatedAt, UpdatedAt)



VALUES 



    (NEWID(), 'E2E00000-0000-0000-0000-00000000008A', 'E2E00000-0000-0000-0000-000000000001', 0, GETUTCDATE(), GETUTCDATE()),



    (NEWID(), 'E2E00000-0000-0000-0000-00000000008A', 'E2E00000-0000-0000-0000-00000000000D', 10000, GETUTCDATE(), GETUTCDATE());







INSERT INTO GroupDeletionRequests (Id, GroupId, OwnerUserId, Status, FundBalanceSnapshot, ReadyAt, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000008B', 'E2E00000-0000-0000-0000-00000000008A', 'E2E00000-0000-0000-0000-000000000001', 'Ready', 10000, GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO GroupDeletionVotes (Id, GroupDeletionRequestId, UserId, UserNameSnapshot, Status, VotedAt, CreatedAt, UpdatedAt)



VALUES 



    (NEWID(), 'E2E00000-0000-0000-0000-00000000008B', 'E2E00000-0000-0000-0000-000000000001', 'Fixture User', 'Approved', GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),



    (NEWID(), 'E2E00000-0000-0000-0000-00000000008B', 'E2E00000-0000-0000-0000-00000000000D', 'Fixture Member', 'Approved', GETUTCDATE(), GETUTCDATE(), GETUTCDATE());







INSERT INTO GroupRefunds (Id, GroupDeletionRequestId, UserId, UserNameSnapshot, ContributionBasis, RefundAmount, Percentage, Status, CreatedAt, UpdatedAt)



VALUES ('E2E00000-0000-0000-0000-00000000008C', 'E2E00000-0000-0000-0000-00000000008B', 'E2E00000-0000-0000-0000-00000000000D', 'Fixture Member', 10000, 10000, 100.0, 'Pending', GETUTCDATE(), GETUTCDATE());



"@



$fCmd.ExecuteNonQuery() | Out-Null







# Align Fund 2 and Fund 3 ActivityClosedAt ticks with Elections InactivityEvidenceId



$getDt2Cmd = $conn.CreateCommand()



$getDt2Cmd.CommandText = "SELECT ActivityClosedAt FROM V1Funds WHERE Id = 'E2E00000-0000-0000-0000-00000000001C'"



$fund2Dt = $getDt2Cmd.ExecuteScalar()



$fund2Ticks = $fund2Dt.Ticks



$updEvCmd2 = $conn.CreateCommand()



$updEvCmd2.CommandText = "UPDATE V1Elections SET InactivityEvidenceId = 'owner-response:$fund2Ticks' WHERE Id = 'E2E00000-0000-0000-0000-00000000000F'"



$updEvCmd2.ExecuteNonQuery() | Out-Null







$getDt3Cmd = $conn.CreateCommand()



$getDt3Cmd.CommandText = "SELECT ActivityClosedAt FROM V1Funds WHERE Id = 'E2E00000-0000-0000-0000-00000000002C'"



$fund3Dt = $getDt3Cmd.ExecuteScalar()



$fund3Ticks = $fund3Dt.Ticks



$updEvCmd3 = $conn.CreateCommand()



$updEvCmd3.CommandText = "UPDATE V1Elections SET InactivityEvidenceId = 'owner-response:$fund3Ticks' WHERE Id = 'E2E00000-0000-0000-0000-000000000010'"



$updEvCmd3.ExecuteNonQuery() | Out-Null







$conn.Close()







# Run C# helper to ensure exact PolicyJson string escaping



dotnet run --project scratch/FixPolicy/FixPolicy.csproj | Out-Null







Write-Host "Database seed complete. Authenticating users via Kestrel..."







# Log in UserFixture



$login1 = @{ identifier = "fixture_user@spendwise.test"; password = "StrongPass1!"; deviceName = "Fixture" } | ConvertTo-Json



$r1 = Invoke-RestMethod -Uri "http://127.0.0.1:5158/api/auth/login" -Method Post -Body $login1 -ContentType "application/json"



$verify1 = @{ challengeToken = $r1.challengeToken; pin = "123456"; deviceName = "Fixture" } | ConvertTo-Json



$v1 = Invoke-RestMethod -Uri "http://127.0.0.1:5158/api/auth/pin/verify" -Method Post -Body $verify1 -ContentType "application/json"



$userToken = $v1.accessToken



Write-Host "UserFixture Token acquired."







# Log in MemberFixture



$login2 = @{ identifier = "fixture_member@spendwise.test"; password = "StrongPass1!"; deviceName = "Fixture" } | ConvertTo-Json



$r2 = Invoke-RestMethod -Uri "http://127.0.0.1:5158/api/auth/login" -Method Post -Body $login2 -ContentType "application/json"



$verify2 = @{ challengeToken = $r2.challengeToken; pin = "123456"; deviceName = "Fixture" } | ConvertTo-Json



$v2 = Invoke-RestMethod -Uri "http://127.0.0.1:5158/api/auth/pin/verify" -Method Post -Body $verify2 -ContentType "application/json"



$memberToken = $v2.accessToken



Write-Host "MemberFixture Token acquired."







# Update postman environments



$envFiles = @(



    "e:\source\SpendWise-final-merged\SpendWise-Test.postman_environment.json",



    "e:\source\SpendWise-final-merged\backend\postman\SpendWise-Test.postman_environment.json"



)







$newVars = @{



    "fixture_user_id" = "E2E00000-0000-0000-0000-000000000001"



    "fixture_user_spendId" = "e2e_fixture_user"



    "fixture_user_phone" = "0900000001"



    "fixture_user_token" = $userToken



    "fixture_member_id" = "E2E00000-0000-0000-0000-00000000000D"



    "fixture_member_spendId" = "e2e_fixture_member"



    "fixture_member_token" = $memberToken



    "fixture_wallet_id" = "E2E00000-0000-0000-0000-000000000003"



    "fixture_wallet2_id" = "E2E00000-0000-0000-0000-000000000023"



    "fixture_bank_connection_id" = "E2E00000-0000-0000-0000-000000000005"



    "fixture_bank_transaction_id" = "E2E00000-0000-0000-0000-000000000006"



    "fixture_case_id" = "E2E00000-0000-0000-0000-000000000007"



    "fixture_case_attachment_id" = "E2E00000-0000-0000-0000-000000000008"



    "fixture_dispute_id" = "E2E00000-0000-0000-0000-000000000091"



    "fixture_ai_action_execute_id" = "E2E00000-0000-0000-0000-000000000009"



    "fixture_ai_action_cancel_id" = "E2E00000-0000-0000-0000-00000000000A"



    "fixture_ai_attachment_id" = "E2E00000-0000-0000-0000-0000000000B1"



    "fixture_pin_reset_token" = $rawToken



    "fixture_fixed_fund_id" = "E2E00000-0000-0000-0000-00000000000C"



    "fixture_fixed_fund2_id" = "E2E00000-0000-0000-0000-00000000000C"



    "fixture_fixed_fund3_id" = "E2E00000-0000-0000-0000-00000000002C"



    "fixedFund2Id" = "E2E00000-0000-0000-0000-00000000001C"



    "heldExpenseId" = "E2E00000-0000-0000-0000-000000000095"



    "fixture_election_id" = "E2E00000-0000-0000-0000-00000000000E"



    "fixture_election_accept_id" = "E2E00000-0000-0000-0000-000000000010"



    "fixture_election_reject_id" = "E2E00000-0000-0000-0000-000000000020"



    "fixture_election_ballot_id" = "E2E00000-0000-0000-0000-00000000001E"



    "fixture_activity_proposal_id" = "E2E00000-0000-0000-0000-000000000011"



    "fixture_activity_vote_id" = "E2E00000-0000-0000-0000-000000000012"



    "fixture_fund4_activity_id" = "E2E00000-0000-0000-0000-00000000003C"



    "fixture_fund5_election_id" = "E2E00000-0000-0000-0000-00000000004C"



    "fixture_recurring_id" = "E2E00000-0000-0000-0000-000000000080"



    "fixture_occurrence_id" = "E2E00000-0000-0000-0000-000000000081"



    "fixture_evidence_id" = "E2E00000-0000-0000-0000-000000000085"



    "fixture_friend_req_cancel_id" = "E2E00000-0000-0000-0000-000000000071"



    "fixture_friend_req_reject_id" = "E2E00000-0000-0000-0000-000000000072"



    "fixture_friendship_id" = "E2E00000-0000-0000-0000-000000000073"



    "fixture_unfriend_user_id" = "E2E00000-0000-0000-0000-000000000002"



    "fixture_invitation_review_id" = "E2E00000-0000-0000-0000-0000000000A1"



    "fixture_invitation_accept_id" = "E2E00000-0000-0000-0000-0000000000A2"



    "fixture_invitation_reject_id" = "E2E00000-0000-0000-0000-0000000000A2"



    "fixture_invitation_cancel_id" = "E2E00000-0000-0000-0000-0000000000A4"



    "fixture_ownership_offer_id" = "E2E00000-0000-0000-0000-000000000099"



    "fixture_reject_invite_id" = "E2E00000-0000-0000-0000-000000000088"



    "fixture_ops_group_id" = "E2E00000-0000-0000-0000-000000000089"



    "fixture_remove_member_id" = "E2E00000-0000-0000-0000-000000000002"



    "fixture_refund_group_id" = "E2E00000-0000-0000-0000-00000000008A"



    "fixture_refund_request_id" = "E2E00000-0000-0000-0000-00000000008B"



    "fixture_refund_id" = "E2E00000-0000-0000-0000-00000000008C"



    "fixture_propose_activity_fund_id" = "E2E00000-0000-0000-0000-00000000009C"



    "fixture_member_phone" = "0900000002"
    "fixture_bank_connection_unlink_id" = "E2E00000-0000-0000-0000-000000000025"
    "fixture_pending_wallet_top_up_id" = "E2E00000-0000-0000-0000-000000000050"



}







foreach ($envFile in $envFiles) {



    if (Test-Path $envFile) {



        $envJson = Get-Content $envFile -Raw | ConvertFrom-Json



        foreach ($key in $newVars.Keys) {



            $existing = $envJson.values | Where-Object { $_.key -eq $key }



            if ($existing) {



                $existing.value = $newVars[$key]



                $existing.enabled = $true



            } else {



                $envJson.values += [PSCustomObject]@{



                    key = $key



                    value = $newVars[$key]



                    enabled = $true



                }



            }



        }



        $envJson | ConvertTo-Json -Depth 10 | Set-Content $envFile -Encoding UTF8



        Write-Host "Updated environment at $envFile"



    }



}











Write-Host "All acceptance fixtures seeded cleanly and environment variables updated!"



