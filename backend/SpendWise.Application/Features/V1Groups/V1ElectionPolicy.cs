using SpendWise.Application.Common.Exceptions;

namespace SpendWise.Application.Features.V1Groups;

public sealed class V1ElectionPolicy
{
    public string? Version { get; set; }
    public int? RegistrationSeconds { get; set; }
    public int? NominationSeconds { get; set; }
    public int? RunoffSeconds { get; set; }
    public int? ConfirmationSeconds { get; set; }
    public int? AcceptanceSeconds { get; set; }
    public bool? FreezeMembership { get; set; }
    public bool? AllowContributions { get; set; }
    public string? NoCandidatesOutcome { get; set; }
    public string? NoVotesOutcome { get; set; }
    public string? AcceptanceExpiryOutcome { get; set; }
    public string? EligibilityChangeOutcome { get; set; }
    public V1ElectionPolicySnapshot Snapshot()
    {
        if (string.IsNullOrWhiteSpace(Version) || Version.Length > 100 || RegistrationSeconds is null or <= 0 || NominationSeconds is null or <= 0
            || RunoffSeconds is null or <= 0 || ConfirmationSeconds is null or <= 0 || AcceptanceSeconds is null or <= 0
            || FreezeMembership != true || AllowContributions is null || NoCandidatesOutcome != "Fail" || NoVotesOutcome != "Fail" || AcceptanceExpiryOutcome != "Fail" || EligibilityChangeOutcome != "Fail")
            throw new ConflictException("POLICY_BLOCKED", "An explicitly approved complete election policy is required.");
        return new(Version, RegistrationSeconds.Value, NominationSeconds.Value, RunoffSeconds.Value,
            ConfirmationSeconds.Value, AcceptanceSeconds.Value, AllowContributions.Value);
    }
}
public sealed record V1ElectionPolicySnapshot(string Version, int RegistrationSeconds, int NominationSeconds, int RunoffSeconds, int ConfirmationSeconds, int AcceptanceSeconds, bool AllowContributions);
public sealed record V1InactivityProof(bool CoverageComplete, bool Eligible, string? EvidenceId);
public interface IV1InactivityEvidence { Task<V1InactivityProof> GetAsync(Guid userId, CancellationToken ct); }
public sealed class IncompleteInactivityEvidence : IV1InactivityEvidence
{
    public Task<V1InactivityProof> GetAsync(Guid userId, CancellationToken ct) => Task.FromResult(new V1InactivityProof(false, false, null));
}
public sealed record V1OpenElectionRequest(long ExpectedOwnershipRevision);
public sealed record V1BallotRequest(int Round, Guid? CandidateUserId, bool? Support);
public sealed record V1BallotReceipt(Guid Id, int Round, bool Replayed);
public sealed record V1ElectionDto(Guid Id, Guid FundId, string Phase, long OwnershipRevision, Guid? WinnerUserId, DateTime PhaseStartedAt, DateTime? FinishedAt, string? FailureReason);
public sealed record V1ElectionView(V1ElectionDto Election, IReadOnlyList<Guid> Candidates, int ElectorateSize, bool IsElector, V1BallotReceipt? OwnBallot);
