using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Identity;
using SpendWise.Application.Features.Wallets;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Domain.Planning;

namespace SpendWise.Infrastructure.Persistence;

// Tạo dữ liệu mẫu có kiểm soát chỉ cho môi trường Development.

public static class DevelopmentSeeder
{
    public static async Task SeedAsync(IServiceProvider root, CancellationToken ct = default)
    {
        using var scope = root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        await db.Database.MigrateAsync(ct);
        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        var password = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var users = new[]
        {
            User("Nguyễn Minh Anh", "minhanh@spendwise.local", "minhanh", password),
            User("Nguyễn Văn Hoàng", "hoang@spendwise.local", "hoang", password),
            User("Phạm Minh Quang", "quang@spendwise.local", "quang", password),
            User("Trần Quốc Nam", "nam@spendwise.local", "nam", password),
            User("Trần Mai Anh", "maianh@spendwise.local", "maianh", password),
            User(
                "SpendWise Administrator",
                "lethien241205@gmail.com",
                "lethien241205",
                password,
                SystemRole.Admin)
        };
        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);

        var balances = new[]
        {
            5_000_000m,
            3_000_000m,
            2_500_000m,
            2_000_000m,
            4_000_000m,
            0m
        };
        var wallets = new List<Wallet>();
        for (var index = 0; index < users.Length; index++)
        {
            var wallet = WalletFactory.Create(
                users[index].Id,
                "Ví chính",
                WalletType.Main,
                true);
            wallet.AvailableBalance = balances[index];
            wallet.LedgerAccount.Balance = balances[index];
            wallets.Add(wallet);
        }

        db.Wallets.AddRange(wallets);
        // Danh mục hệ thống do migration seed bằng ID/Key cố định; seeder không tạo bản sao.
        var food = await db.Categories.SingleAsync(x => x.Key == "categories.food", ct);
        db.Budgets.Add(new Budget
        {
            OwnerUserId = users[0].Id,
            CategoryId = food.Id,
            Category = food,
            Year = DateTime.UtcNow.Year,
            Month = DateTime.UtcNow.Month,
            LimitAmount = 3_000_000
        });
        var goalAccount = new LedgerAccount
        {
            Code = $"GOAL:{Guid.NewGuid():N}",
            Name = "Quỹ khẩn cấp",
            Type = LedgerAccountType.GoalReserve,
            OwnerUserId = users[0].Id,
            Balance = 1_000_000
        };
        db.LedgerAccounts.Add(goalAccount);
        db.Goals.Add(new Goal
        {
            OwnerUserId = users[0].Id,
            Name = "Quỹ khẩn cấp",
            TargetAmount = 20_000_000,
            CurrentAllocatedAmount = 1_000_000,
            LedgerAccount = goalAccount,
            LedgerAccountId = goalAccount.Id,
            TargetDate = DateTime.UtcNow.AddYears(1)
        });

        var group = new Group
        {
            Name = "Phòng trọ 101",
            Description = "Chi phí sinh hoạt chung",
            Type = GroupType.Roommates
        };
        group.Members = users
            .Take(4)
            .Select((user, index) => new GroupMember
            {
                UserId = user.Id,
                Role = index == 0 ? GroupRole.Owner : GroupRole.Member,
                JoinedAt = DateTime.UtcNow
            })
            .ToList();
        var fundAccount = new LedgerAccount
        {
            Code = $"GROUP-FUND:{group.Id:N}",
            Name = "Phòng trọ 101 Shared Fund",
            Type = LedgerAccountType.SharedFund,
            GroupId = group.Id,
            Balance = 2_000_000
        };
        group.SharedFund = new SharedFund
        {
            GroupId = group.Id,
            LedgerAccount = fundAccount,
            LedgerAccountId = fundAccount.Id,
            Balance = 2_000_000
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync(ct);

        db.Notifications.AddRange(
            new Notification
            {
                UserId = users[0].Id,
                Type = NotificationType.SystemAnnouncement,
                Title = "Chào mừng",
                Message = "Chào mừng bạn đến với SpendWise."
            },
            new Notification
            {
                UserId = users[1].Id,
                Type = NotificationType.SystemAnnouncement,
                Title = "Nhóm mẫu",
                Message = "Bạn đã được thêm vào nhóm Development Phòng trọ 101."
            });

        await db.SaveChangesAsync(ct);
    }

    private static User User(
        string name,
        string email,
        string spendId,
        IPasswordService password,
        SystemRole role = SystemRole.User)
    {
        // Tạo dữ liệu người dùng mẫu với định danh duy nhất cho Development.
        return new User
        {
            FullName = name,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SpendId = spendId,
            NormalizedSpendId = spendId.ToUpperInvariant(),
            PasswordHash = password.Hash("Demo@12345"),
            SystemRole = role,
            // Demo User and Admin seeded without registration verification.
            EmailVerifiedAt = DateTime.UtcNow
        };
    }
}
