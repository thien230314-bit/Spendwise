using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using SpendWise.Infrastructure.Identity;

namespace SpendWise.Tests;

// Fixture SQLite dùng chung cho các test correctness cần transaction và khóa ngoại thật.
internal sealed class TargetedCorrectnessFixture : IAsyncDisposable
{
    public const string SecurityPin = "123456";
    private readonly SqliteConnection connection;

    private TargetedCorrectnessFixture(
        SqliteConnection connection,
        SpendWiseDbContext db,
        IClock clock)
    {
        this.connection = connection;
        Db = db;
        Clock = clock;
        Security = new FinancialSecurityGuard(db, new SecurityPinService(),
            new AccountCredentialLockout(db, clock));
    }

    public SpendWiseDbContext Db { get; }
    public IClock Clock { get; }
    public FinancialSecurityGuard Security { get; }

    public static async Task<TargetedCorrectnessFixture> CreateAsync(IClock? suppliedClock = null)
    {
        // Giữ kết nối mở để cơ sở dữ liệu in-memory tồn tại suốt một test.
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var clock = suppliedClock ?? new SystemClock();
        var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new SpendWiseDbContext(options, clock);
        await db.Database.EnsureCreatedAsync();
        return new TargetedCorrectnessFixture(connection, db, clock);
    }

    public SpendWiseDbContext CreateNewDbContext()
    {
        var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseSqlite(connection)
            .Options;
        return new SpendWiseDbContext(options, Clock);
    }

    public async Task<User> AddUserAsync(
        string name,
        UserStatus status = UserStatus.Active,
        SystemRole role = SystemRole.User,
        bool linkActiveBank = true)
    {
        // Mỗi test dùng email và SpendId riêng để không vi phạm unique index.
        var unique = Guid.NewGuid().ToString("N");
        var email = $"{unique}@test.local";
        var user = new User
        {
            FullName = name,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SpendId = unique,
            NormalizedSpendId = unique.ToUpperInvariant(),
            PasswordHash = "test-hash",
            SecurityPinHash = new SecurityPinService().Hash(SecurityPin),
            Status = status,
            SystemRole = role
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        if (linkActiveBank)
        {
            var bankConnection = new BankConnection
            {
                UserId = user.Id,
                Provider = BankProvider.SePayBankHub,
                AccountEnvironment = SePayAccountEnvironment.TestMode,
                ExternalAccountId = $"ACC_{unique}",
                BankBrandName = "MBBank",
                AccountHolderName = name,
                MaskedAccountNumber = "****1234",
                Status = BankConnectionStatus.Active,
                LinkedAt = DateTime.UtcNow
            };
            Db.BankConnections.Add(bankConnection);
            await Db.SaveChangesAsync();
        }

        return user;
    }

    public async Task<Wallet> AddWalletAsync(
        Guid ownerUserId,
        string name,
        bool isDefault,
        WalletStatus status = WalletStatus.Active,
        decimal balance = 0,
        WalletType type = WalletType.Main)
    {
        // WalletFactory tạo luôn LedgerAccount; test chỉ đặt trạng thái và số dư ban đầu.
        var wallet = WalletFactory.Create(ownerUserId, name, type, isDefault);
        wallet.Status = status;
        wallet.AvailableBalance = balance;
        wallet.LedgerAccount.Balance = balance;
        Db.Wallets.Add(wallet);
        await Db.SaveChangesAsync();
        return wallet;
    }

    public async Task<UserSession> AddSessionAsync(
        Guid userId,
        DateTime? revokedAt = null)
    {
        // Session liên kết trực tiếp với UserId để validator kiểm tra đúng chủ sở hữu.
        var session = new UserSession
        {
            UserId = userId,
            DeviceName = "Test browser",
            LastSeenAt = Clock.UtcNow,
            RevokedAt = revokedAt
        };
        Db.UserSessions.Add(session);
        await Db.SaveChangesAsync();
        return session;
    }

    public async Task<RefreshToken> AddRefreshTokenAsync(
        Guid userId,
        string tokenHash,
        DateTime? revokedAt = null,
        Guid? userSessionId = null,
        DateTime? expiresAt = null)
    {
        // Mọi refresh token test đều thuộc một session thật như flow Login/Register.
        var sessionId = userSessionId;
        if (sessionId is null)
        {
            sessionId = await Db.UserSessions
                .Where(session => session.UserId == userId)
                .Select(session => (Guid?)session.Id)
                .FirstOrDefaultAsync();
        }
        if (sessionId is null)
        {
            sessionId = (await AddSessionAsync(userId)).Id;
        }

        var token = new RefreshToken
        {
            UserId = userId,
            UserSessionId = sessionId.Value,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt ?? Clock.UtcNow.AddDays(1),
            RevokedAt = revokedAt,
            DeviceName = "Test browser"
        };
        Db.RefreshTokens.Add(token);
        await Db.SaveChangesAsync();
        return token;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await connection.DisposeAsync();
    }
}

// Token service xác định giúp test refresh không phụ thuộc khóa ký JWT thật.
internal sealed class TargetedTokenService : ITokenService
{
    private int refreshSequence;

    public AccessTokenResult CreateAccessToken(User user, Guid sessionId) =>
        new("test-access-token", DateTime.UtcNow.AddMinutes(15));

    public string CreateRefreshToken() => $"replacement-{++refreshSequence}";

    public string HashRefreshToken(string token) => token;
}
