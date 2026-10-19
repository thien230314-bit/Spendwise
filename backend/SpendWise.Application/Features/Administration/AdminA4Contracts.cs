using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.Administration;

public sealed record A4Query(string Reason, string? Type = null, string? Status = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null, int Page = 1, int PageSize = 20);

public sealed record A4TransactionItem
{
    public Guid Id { get; init; }
    public string BusinessType { get; init; } = "";
    public string Status { get; init; } = "";
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "VND";
    public DateTime OccurredAt { get; init; }
    public Guid? WalletId { get; init; }
    public string? WalletName { get; init; }
    public Guid? LedgerTransactionId { get; init; }
    public string? LedgerStatus { get; init; }
    public string? LedgerReference { get; init; }
    public Guid? RelatedBusinessId { get; init; }
    public string? RelatedBusinessSource { get; init; }
    public string? Counterparty { get; init; }
    public string? Description { get; init; }
}

public sealed record A4LookupResult
{
    public Guid Id { get; init; }
    public string QueryCode { get; init; } = "";
    public string MatchedBy { get; init; } = "";
    public string BusinessType { get; init; } = "";
    public string Status { get; init; } = "";
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "VND";
    public DateTime OccurredAt { get; init; }
    public Guid? WalletId { get; init; }
    public string? WalletName { get; init; }
    public string? WalletType { get; init; }
    public Guid? LedgerTransactionId { get; init; }
    public string? LedgerStatus { get; init; }
    public string? LedgerReference { get; init; }
    public Guid? RelatedBusinessId { get; init; }
    public string? RelatedBusinessSource { get; init; }
    public string MaskedCounterparty { get; init; } = "";
    public string MaskedDescription { get; init; } = "";
}

public sealed record A4UserWalletsResult(
    IReadOnlyList<A4WalletItem> Wallets,
    IReadOnlyList<A4LinkedBankItem> LinkedBanks);

public sealed record A4WalletItem(
    Guid Id,
    string Name,
    string Type,
    string Status,
    string Currency,
    decimal AvailableBalance,
    bool IsDefault,
    Guid? LedgerAccountId,
    string ConstraintNotice);

public sealed record A4LinkedBankItem(
    Guid Id,
    string BankBrandName,
    string MaskedAccountNumber,
    string? MaskedAccountHolderName,
    string Status,
    DateTime? LinkedAt);

public sealed record A4PendingFailedTransaction(
    Guid Id,
    string Source,
    string BusinessType,
    string Status,
    decimal Amount,
    string Currency,
    DateTime CreatedAt,
    DateTime? FailedOrCancelledAt,
    string? FailureOrReviewReason,
    string InvestigationNote,
    IReadOnlyList<A4RetryHistoryItem> RetryHistory);

public sealed record A4RetryHistoryItem(
    DateTime TimestampUtc,
    string Action,
    string Outcome,
    string Detail);

public sealed record A4LedgerEntryDetail(
    Guid Id,
    Guid LedgerAccountId,
    string AccountName,
    string AccountCode,
    string AccountType,
    string Direction,
    decimal Amount,
    string Currency,
    string Memo);

public sealed record A4LedgerDetailResult(
    Guid LedgerTransactionId,
    string Reference,
    string BusinessType,
    Guid BusinessId,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string BalanceStatus,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    string BusinessBindingStatus,
    IReadOnlyList<A4LedgerEntryDetail> Entries);

public sealed record A4TraceTimelineItem(
    int Step,
    string Stage,
    DateTime? TimestampUtc,
    string Actor,
    string Status,
    string ReferenceOrCorrelation,
    string SourceRecord,
    string Details);

public sealed record A4TraceResult(
    Guid TransactionId,
    string BusinessType,
    string OverallStatus,
    IReadOnlyList<A4TraceTimelineItem> Timeline);
