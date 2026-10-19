using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.V1Groups;
using SpendWise.Domain.Identity;

namespace SpendWise.Infrastructure.Persistence.Configurations;

public sealed class V1ActivityProposalConfiguration : IEntityTypeConfiguration<V1ActivityProposal>
{
    public void Configure(EntityTypeBuilder<V1ActivityProposal> b)
    {
        b.Property(p => p.State).HasMaxLength(40); b.Property(p => p.IdempotencyKey).HasMaxLength(100);
        b.HasIndex(p => new { p.FundId, p.IdempotencyKey }).IsUnique();
        b.HasIndex(p => p.FundId).IsUnique().HasFilter("[State] IN ('Voting', 'ApprovedAwaitingSettlement')");
        b.HasOne<V1Fund>().WithMany().HasForeignKey(p => p.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(p => p.ProposedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class V1ActivityVoterConfiguration : IEntityTypeConfiguration<V1ActivityVoter>
{
    public void Configure(EntityTypeBuilder<V1ActivityVoter> b)
    {
        b.HasIndex(v => new { v.ProposalId, v.UserId }).IsUnique();
        b.HasOne<V1ActivityProposal>().WithMany().HasForeignKey(v => v.ProposalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<V1FundMembership>().WithMany().HasForeignKey(v => v.MembershipId).OnDelete(DeleteBehavior.Restrict);
    }
}
