namespace SpendWise.Application.AI;

public enum ContentSeverity { None, Mild, Severe }
public enum ContentAction { Allow, Warning, Mask, Reject }
public sealed record ContentSafetyResult(string NormalizedText, ContentSeverity Severity, ContentAction Action, string SafeText);
public sealed record CreateAiConversationRequest(string? Title = null, string ContextType = "General", Guid? ContextGroupId = null);
public sealed record UpdateAiConversationRequest(string? Title = null, bool? IsPinned = null, string? ContextType = null, Guid? ContextGroupId = null);
public sealed record AiChatRequest(Guid? ConversationId, string Message, string Mode = "FinancialQuery", Guid? GroupId = null,
    IReadOnlyList<Guid>? AttachmentIds = null);
public sealed record AiAttachmentDto(Guid Id, string OriginalFileName, string ContentType, long Length, string ContentHash, DateTime CreatedAt);
public sealed record AiMessageDto(Guid Id, string Role, string Content, DateTime CreatedAt, IReadOnlyList<AiAttachmentDto>? Attachments = null);
public sealed record AiConversationDto(Guid Id, string Title, DateTime CreatedAt, DateTime UpdatedAt, bool IsPinned,
    string ContextType, Guid? ContextGroupId, IReadOnlyList<AiMessageDto> Messages);
public sealed record AiProposedActionDto(Guid ActionId, string ActionType, string SafeSummary, bool RequiresPin,
    bool RequiresEvidence, bool RequiresExplicitConfirmation, DateTime ExpiresAt);
public sealed record AiChatResponse(Guid ConversationId, string Message, string Provider, bool IsFallback,
    AiProposedActionDto? ProposedAction = null);
public sealed record AiPromptMessage(string Role, string Content);
public sealed record AiCompletionResult(string Message, string Provider, bool IsFallback, AiProposedActionDto? ProposedAction = null);
public sealed record AiFinancialContext(decimal TotalBalance, decimal MonthlyExpense, IReadOnlyDictionary<string, decimal> ExpensesByCategory,
    IReadOnlyList<string> BudgetStatus, IReadOnlyList<string> GoalProgress, string? SharedFundStatus);
public sealed record AiToolDefinition(string Name, string Description, string JsonSchema);
public sealed record AiToolCall(string Name, string ArgumentsJson);
public sealed record AiProviderAttachment(string ContentType, string FileName, byte[]? ImageBytes, string? ExtractedText);
public sealed record AiProviderRequest(string SystemRole, string Locale, AiFinancialContext Context,
    IReadOnlyCollection<AiPromptMessage> History, string CurrentMessage, IReadOnlyCollection<AiProviderAttachment> Attachments,
    IReadOnlyCollection<AiToolDefinition> Tools, Func<AiToolCall, CancellationToken, Task<string>> ExecuteToolAsync,
    string? SystemInstruction = null, string? AuthorizedContextJson = null);
public sealed record UploadAiAttachmentData(string OriginalFileName, string ContentType, byte[] Content);
public sealed record ExecuteAiActionRequest(string? SecurityPin = null, IReadOnlyList<Guid>? EvidenceIds = null,
    bool ExplicitConfirmation = false);
public sealed record AiActionDto(Guid Id, string ActionType, string SafeSummary, string Status, DateTime ExpiresAt,
    DateTime? ExecutedAt, string? FailureCode, object? Result = null);

public interface IProfanityFilter { ContentSafetyResult Evaluate(string input); }
// Cho health endpoint biết AI online có được cấu hình hay đang dùng fallback.
public interface IAiProviderStatus { string Status { get; } bool IsOnlineConfigured { get; } }
public interface IAiDataTools { Task<AiFinancialContext> BuildContextAsync(Guid userId, Guid? groupId, CancellationToken cancellationToken); }
public interface IAiToolRegistry
{
    IReadOnlyCollection<AiToolDefinition> Definitions { get; }
    Task<string> ExecuteAsync(Guid userId, Guid? conversationId, AiToolCall call, CancellationToken cancellationToken);
}
public interface IAiAttachmentReader
{
    Task<IReadOnlyList<AiProviderAttachment>> ReadAsync(Guid userId, IReadOnlyCollection<Guid> attachmentIds, CancellationToken cancellationToken);
}
public interface IAiCompletionProvider
{
    Task<AiCompletionResult> CompleteAsync(AiProviderRequest request, CancellationToken cancellationToken);
}
