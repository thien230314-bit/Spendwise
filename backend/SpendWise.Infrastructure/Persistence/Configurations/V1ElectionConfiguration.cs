using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.V1Groups;
using SpendWise.Domain.Identity;

namespace SpendWise.Infrastructure.Persistence.Configurations;

public sealed class V1ElectionConfiguration : IEntityTypeConfiguration<V1Election>
{
    public void Configure(EntityTypeBuilder<V1Election> b)
    {
        b.HasIndex(e => e.FundId).IsUnique().HasFilter("[FinishedAt] IS NULL");
        b.Property(e => e.PolicyJson).HasMaxLength(4000); b.Property(e => e.InactivityEvidenceId).HasMaxLength(200); b.Property(e => e.FailureReason).HasMaxLength(100);
        b.HasOne<V1Fund>().WithMany().HasForeignKey(e => e.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.IncumbentUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.OpenedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class V1ElectionVoterConfiguration : IEntityTypeConfiguration<V1ElectionVoter>
{
    public void Configure(EntityTypeBuilder<V1ElectionVoter> b)
    {
        b.HasIndex(v => new { v.ElectionId, v.UserId }).IsUnique();
        b.HasOne<V1Election>().WithMany().HasForeignKey(v => v.ElectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<V1FundMembership>().WithMany().HasForeignKey(v => v.MembershipId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class V1ElectionCandidateConfiguration : IEntityTypeConfiguration<V1ElectionCandidate>
{
    public void Configure(EntityTypeBuilder<V1ElectionCandidate> b)
    {
        b.HasIndex(c => new { c.ElectionId, c.UserId }).IsUnique();
        b.HasOne<V1Election>().WithMany().HasForeignKey(c => c.ElectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class V1ElectionBallotConfiguration : IEntityTypeConfiguration<V1ElectionBallot>
{
    public void Configure(EntityTypeBuilder<V1ElectionBallot> b)
    {
        b.HasIndex(v => new { v.ElectionId, v.Round, v.VoterUserId }).IsUnique();
        b.HasOne<V1Election>().WithMany().HasForeignKey(v => v.ElectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(v => v.VoterUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(v => v.CandidateUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
