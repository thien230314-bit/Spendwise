using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Identity;

namespace SpendWise.Infrastructure.Persistence.Configurations;

// File này cấu hình User, refresh token và metadata của phiên đăng nhập.

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Email, phone đã chuẩn hóa và SpendId nội bộ đều có ràng buộc duy nhất ở database.
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.HasIndex(x => x.NormalizedSpendId).IsUnique();
        builder.HasIndex(x => x.Phone)
            .IsUnique()
            .HasFilter("[Phone] IS NOT NULL");
        builder.Property(x => x.FullName).HasMaxLength(160);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.NormalizedEmail).HasMaxLength(256);
        builder.Property(x => x.SpendId).HasMaxLength(50);
        builder.Property(x => x.NormalizedSpendId).HasMaxLength(50);
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.PasswordHash).HasMaxLength(512);
        builder.Property(x => x.SecurityPinHash).HasMaxLength(512);
        builder.Property(x => x.SystemRole)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(x => x.PreferredLanguage).HasMaxLength(10);
        builder.Property(x => x.PreferredTheme).HasMaxLength(20);
        builder.Property(x => x.DefaultCurrency).HasMaxLength(3);
    }
}

public sealed class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequestIpHash).HasMaxLength(64);
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(x => new { x.UserId, x.Purpose, x.CreatedAt });
        builder.HasIndex(x => new { x.RequestIpHash, x.CreatedAt });
        builder.HasOne(x => x.User)
            .WithMany(x => x.EmailVerificationTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LoginChallengeConfiguration : IEntityTypeConfiguration<LoginChallenge>
{
    public void Configure(EntityTypeBuilder<LoginChallenge> builder)
    {
        // TokenHash là duy nhất; challenge hết hạn hoặc đã dùng không thể tạo session.
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt });
        builder.Property(x => x.TokenHash).HasMaxLength(64);
        builder.Property(x => x.Purpose)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(x => x.DeviceName).HasMaxLength(200);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.HasOne(x => x.User)
            .WithMany(x => x.LoginChallenges)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SecurityPinResetTokenConfiguration : IEntityTypeConfiguration<SecurityPinResetToken>
{
    public void Configure(EntityTypeBuilder<SecurityPinResetToken> builder)
    {
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt });
        builder.Property(x => x.TokenHash).HasMaxLength(64);
        builder.Property(x => x.Purpose)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.HasOne(x => x.User)
            .WithMany(x => x.SecurityPinResetTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        // TokenHash là duy nhất và metadata thiết bị/IP hỗ trợ quản lý phiên an toàn.
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.UserSessionId);
        builder.Property(x => x.TokenHash).HasMaxLength(64);
        builder.Property(x => x.DeviceName).HasMaxLength(200);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.HasOne(x => x.UserSession)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        // Lưu metadata thiết bị, IP và UserAgent để người dùng nhận biết từng phiên.
        builder.Property(x => x.DeviceName).HasMaxLength(200);
        builder.Property(x => x.DeviceIdentifier).HasMaxLength(200);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
    }
}

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        // Hash là duy nhất; xóa User sẽ xóa token reset chưa còn ý nghĩa.
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt });
        builder.Property(x => x.TokenHash).HasMaxLength(64);
        builder.HasOne(x => x.User)
            .WithMany(x => x.PasswordResetTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
