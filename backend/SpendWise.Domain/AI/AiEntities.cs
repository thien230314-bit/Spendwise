using SpendWise.Domain.Common;

namespace SpendWise.Domain.AI;

// Các entity này lưu hội thoại và tin nhắn AI theo từng người dùng.

public enum AiMessageRole { User, Assistant }
public enum AiConversationContextType { General, Group, Admin }
public enum AiActionProposalStatus { Pending, Executing, Executed, Expired, Cancelled, Failed }

public sealed class AiConversation : BaseEntity
{
    // UserId giới hạn toàn bộ lịch sử hội thoại cho đúng chủ sở hữu.
    public Guid UserId { get; set; }
    // Title là metadata có thể sửa mà không thay đổi nội dung tin nhắn.
    public string Title { get; set; } = "New conversation";
    public bool IsPinned { get; set; }
    public AiConversationContextType ContextType { get; set; }
    public Guid? ContextGroupId { get; set; }
    public List<AiMessage> Messages { get; set; } = [];
    public List<AiAttachment> Attachments { get; set; } = [];
    public List<AiActionProposal> ActionProposals { get; set; } = [];
}
public sealed class AiMessage : BaseEntity
{
    public Guid AiConversationId { get; set; }
    public AiConversation Conversation { get; set; } = null!;
    public AiMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public int? LatencyMs { get; set; }
    public List<AiAttachment> Attachments { get; set; } = [];
}

public sealed class AiAttachment : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public Guid? AiConversationId { get; set; }
    public AiConversation? Conversation { get; set; }
    public Guid? AiMessageId { get; set; }
    public AiMessage? Message { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
}

public sealed class AiActionProposal : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public Guid? AiConversationId { get; set; }
    public AiConversation? Conversation { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string SafeSummary { get; set; } = string.Empty;
    public AiActionProposalStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string? FailureCode { get; set; }
    public string OperationKey { get; set; } = string.Empty;
}
