using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.AI;

namespace SpendWise.Infrastructure.Persistence.Configurations;

// File này cấu hình quan hệ giữa hội thoại AI và các message của người dùng.

public sealed class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        // Index theo UserId và CreatedAt hỗ trợ tải lịch sử hội thoại của đúng người dùng.
        builder.Property(x => x.Title).HasMaxLength(160);
        builder.Property(x => x.ContextType).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.UserId, x.IsPinned, x.UpdatedAt });
    }
}

public sealed class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        // Message thuộc một Conversation và không cascade delete để tránh mất lịch sử ngoài ý muốn.
        builder.Property(x => x.Role)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(x => x.Content).HasMaxLength(8000);
        builder.Property(x => x.Provider).HasMaxLength(50);
        builder.HasOne(x => x.Conversation)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.AiConversationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiAttachmentConfiguration : IEntityTypeConfiguration<AiAttachment>
{
    public void Configure(EntityTypeBuilder<AiAttachment> builder)
    {
        builder.Property(x => x.OriginalFileName).HasMaxLength(255);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.ContentHash).HasMaxLength(64);
        builder.HasIndex(x => new { x.OwnerUserId, x.CreatedAt });
        builder.HasOne(x => x.Conversation).WithMany(x => x.Attachments).HasForeignKey(x => x.AiConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Message).WithMany(x => x.Attachments).HasForeignKey(x => x.AiMessageId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiActionProposalConfiguration : IEntityTypeConfiguration<AiActionProposal>
{
    public void Configure(EntityTypeBuilder<AiActionProposal> builder)
    {
        builder.Property(x => x.ActionType).HasMaxLength(80);
        builder.Property(x => x.PayloadJson).HasMaxLength(8000);
        builder.Property(x => x.SafeSummary).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FailureCode).HasMaxLength(100);
        builder.Property(x => x.OperationKey).HasMaxLength(100);
        builder.HasIndex(x => new { x.OwnerUserId, x.Status, x.ExpiresAt });
        builder.HasOne(x => x.Conversation).WithMany(x => x.ActionProposals).HasForeignKey(x => x.AiConversationId).OnDelete(DeleteBehavior.Restrict);
    }
}
