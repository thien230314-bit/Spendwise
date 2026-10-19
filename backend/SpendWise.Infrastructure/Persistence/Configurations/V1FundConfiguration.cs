using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Infrastructure.Persistence.Configurations;

public sealed class V1FundConfiguration : IEntityTypeConfiguration<V1Fund>
{
    public void Configure(EntityTypeBuilder<V1Fund> b)
    {
        b.HasIndex(f => f.Code).IsUnique();
        b.Property(f => f.Code).HasMaxLength(32);
        b.Property(f => f.Name).HasMaxLength(160);
        b.Property(f => f.Kind).HasConversion<string>().HasMaxLength(10);
        b.HasOne<User>().WithMany().HasForeignKey(f => f.FounderUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(f => f.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SpendWise.Domain.Admin.SupportCase>().WithMany().HasForeignKey(f => f.OwnerResponseCaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class V1FundMembershipConfiguration : IEntityTypeConfiguration<V1FundMembership>
{
    public void Configure(EntityTypeBuilder<V1FundMembership> b)
    {
        b.HasIndex(m => new { m.FundId, m.Seat }).IsUnique().HasFilter("[LeftAt] IS NULL");
        b.HasIndex(m => new { m.FundId, m.UserId }).IsUnique().HasFilter("[LeftAt] IS NULL");
        b.HasIndex(m => new { m.UserId, m.LeftAt });
        b.HasOne<V1Fund>().WithMany().HasForeignKey(m => m.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class V1FundInvitationConfiguration : IEntityTypeConfiguration<V1FundInvitation>
{
    public void Configure(EntityTypeBuilder<V1FundInvitation> b)
    {
        b.Property(i => i.Status).HasConversion<string>().HasMaxLength(12);
        b.HasIndex(i => new { i.FundId, i.InvitedUserId }).IsUnique().HasFilter("[Status] = 'Pending'");
        b.HasOne<V1Fund>().WithMany().HasForeignKey(i => i.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(i => i.InvitedUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(i => i.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class V1OwnershipOfferConfiguration : IEntityTypeConfiguration<V1OwnershipOffer>
{
    public void Configure(EntityTypeBuilder<V1OwnershipOffer> b)
    {
        b.HasOne<V1Fund>().WithMany().HasForeignKey(o => o.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(o => o.FromUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(o => o.ToUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class V1FundActivityConfiguration : IEntityTypeConfiguration<V1FundActivity>
{
    public void Configure(EntityTypeBuilder<V1FundActivity> b)
    {
        b.HasIndex(a => new { a.FundId, a.CreatedAt });
        b.Property(a => a.Event).HasMaxLength(50);
        b.Property(a => a.Detail).HasMaxLength(200);
        b.HasOne<V1Fund>().WithMany().HasForeignKey(a => a.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(a => a.SubjectUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class V1FundCodeCounterConfiguration : IEntityTypeConfiguration<V1FundCodeCounter>
{
    public void Configure(EntityTypeBuilder<V1FundCodeCounter> b)
    {
        b.Property(c => c.Id).ValueGeneratedNever();
        b.HasData(new V1FundCodeCounter { Id = 1, NextValue = 1 });
    }
}
