using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;

namespace SpendWise.Infrastructure.Persistence.Configurations;

public sealed class UserPasskeyCredentialConfiguration : IEntityTypeConfiguration<UserPasskeyCredential>
{
    public void Configure(EntityTypeBuilder<UserPasskeyCredential> builder)
    {
        builder.HasIndex(x => x.CredentialId).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IsRevoked });
        builder.Property(x => x.CredentialId).HasMaxLength(512).IsRequired();
        builder.Property(x => x.PublicKey).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.Algorithm).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DeviceName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Aaguid).HasMaxLength(64);
        builder.Property(x => x.Transports).HasMaxLength(128);

        builder.HasOne(x => x.User)
            .WithMany(x => x.PasskeyCredentials)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PasskeyChallengeConfiguration : IEntityTypeConfiguration<PasskeyChallenge>
{
    public void Configure(EntityTypeBuilder<PasskeyChallenge> builder)
    {
        builder.HasIndex(x => x.Challenge).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Purpose, x.ExpiresAt });
        builder.Property(x => x.Challenge).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.RpId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Origin).HasMaxLength(512);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);
    }
}

public sealed class TransactionAuthorizationConfiguration : IEntityTypeConfiguration<TransactionAuthorization>
{
    public void Configure(EntityTypeBuilder<TransactionAuthorization> builder)
    {
        builder.HasIndex(x => x.Challenge).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Status, x.ExpiresAt });
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey });
        builder.Property(x => x.TransactionType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.SourceId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.DestinationId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Challenge).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.CredentialId).HasMaxLength(512);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
