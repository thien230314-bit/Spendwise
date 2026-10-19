using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Social;

namespace SpendWise.Infrastructure.Persistence.Configurations;

public sealed class FriendRequestConfiguration : IEntityTypeConfiguration<FriendRequest>
{
    public void Configure(EntityTypeBuilder<FriendRequest> builder)
    {
        builder.HasIndex(x => new { x.LowerUserId, x.HigherUserId })
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");
        builder.Property(x => x.Message).HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.RequesterUser).WithMany().HasForeignKey(x => x.RequesterUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AddresseeUser).WithMany().HasForeignKey(x => x.AddresseeUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.HasIndex(x => new { x.LowerUserId, x.HigherUserId }).IsUnique();
        builder.HasOne(x => x.AcceptedRequest).WithMany().HasForeignKey(x => x.AcceptedRequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FriendPreferenceConfiguration : IEntityTypeConfiguration<FriendPreference>
{
    public void Configure(EntityTypeBuilder<FriendPreference> builder)
    {
        builder.HasIndex(x => new { x.FriendshipId, x.UserId }).IsUnique();
        builder.Property(x => x.Nickname).HasMaxLength(160);
        builder.HasOne(x => x.Friendship).WithMany(x => x.Preferences)
            .HasForeignKey(x => x.FriendshipId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
