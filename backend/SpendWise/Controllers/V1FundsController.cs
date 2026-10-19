using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.V1Groups;

namespace SpendWise.API.Controllers;

public sealed record V1InviteRequest(Guid UserId);
public sealed record V1PermissionRequest(bool Enabled, string SecurityPin);
public sealed record V1OwnershipRequest(Guid UserId, string SecurityPin);
public sealed record V1PinRequest(string SecurityPin);
public sealed record V1InvitationReviewRequest(bool Approved);
public sealed record V1ActivityVoteRequest(bool? Support);
public sealed record V1RequestReview(bool? Approved, string Reason, string SecurityPin);

[ApiController]
[Authorize(Policy = "User")]
[Route("api/v1/funds")]
public sealed class V1FundsController(V1FundService funds, ICurrentUser currentUser) : ControllerBase
{
    private Guid Actor => currentUser.UserId ?? throw new ForbiddenException();

    [HttpPost("{id:guid}/requests"), EnableRateLimiting("financial")]
    public Task<V1FundRequestDto> CreateRequest(Guid id, V1FundRequestInput request, CancellationToken ct) => funds.CreateFundRequestAsync(Actor, id, request, ct);
    [HttpGet("{id:guid}/requests")]
    public Task<IReadOnlyList<V1FundRequestDto>> Requests(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => funds.FundRequestsAsync(Actor, id, page, pageSize, ct);
    [HttpPost("requests/{id:guid}/review"), EnableRateLimiting("financial")]
    public Task<V1FundRequestDto> ReviewRequest(Guid id, V1RequestReview request, CancellationToken ct) => funds.ReviewFundRequestAsync(Actor, id,
        request.Approved ?? throw new AppException("explicit_decision_required", "An explicit decision is required."), request.Reason, request.SecurityPin, ct);
    [HttpPost("requests/{id:guid}/cancel"), EnableRateLimiting("financial")]
    public Task<V1FundRequestDto> CancelRequest(Guid id, V1PinRequest request, CancellationToken ct) => funds.CancelFundRequestAsync(Actor, id, request.SecurityPin, ct);

    [HttpGet]
    public Task<IReadOnlyList<V1FundDto>> List(CancellationToken ct) => funds.ListAsync(Actor, ct);

    [HttpPost]
    [EnableRateLimiting("financial")]
    public Task<V1FundDto> Create(CreateV1FundRequest request, CancellationToken ct) => funds.CreateAsync(Actor, request, ct);

    [HttpGet("{id:guid}")]
    public Task<V1FundDto> Get(Guid id, CancellationToken ct) => funds.GetAsync(Actor, id, ct);

    [HttpGet("{id:guid}/members")]
    public Task<IReadOnlyList<V1MemberDto>> Members(Guid id, CancellationToken ct) => funds.MembersAsync(Actor, id, ct);

    [HttpGet("{id:guid}/governance")]
    public Task<V1GovernanceDto> Governance(Guid id, CancellationToken ct) => funds.GovernanceAsync(Actor, id, ct);

    [HttpGet("{id:guid}/owner-response")]
    public Task<V1OwnerResponseDto> OwnerResponse(Guid id, CancellationToken ct) => funds.OwnerResponseAsync(Actor, id, ct);

    [HttpPost("{id:guid}/owner-response/escalate"), EnableRateLimiting("financial")]
    public Task<Guid> EscalateOwnerResponse(Guid id, CancellationToken ct) => funds.EscalateOwnerResponseAsync(Actor, id, ct);

    [HttpPost("{id:guid}/activity/close"), EnableRateLimiting("financial")]
    public async Task<IActionResult> CloseActivity(Guid id, V1PinRequest request, CancellationToken ct)
    { await funds.CloseActivityAsync(Actor, id, request.SecurityPin, ct); return NoContent(); }

    [HttpPost("{id:guid}/activity-proposals"), EnableRateLimiting("financial")]
    public Task<V1ActivityProposalDto> ProposeActivity(Guid id, V1ProposeActivityRequest request, CancellationToken ct) => funds.ProposeActivityAsync(Actor, id, request, ct);

    [HttpGet("activity-proposals/{id:guid}")]
    public Task<V1ActivityProposalDto> ActivityProposal(Guid id, CancellationToken ct) => funds.ActivityProposalAsync(Actor, id, ct);

    [HttpPost("activity-proposals/{id:guid}/vote"), EnableRateLimiting("financial")]
    public Task<V1ActivityProposalDto> VoteActivity(Guid id, V1ActivityVoteRequest request, CancellationToken ct)
    {
        if (request.Support is not { } support) throw new AppException("invalid_ballot", "An explicit Yes or No is required.");
        return funds.VoteActivityAsync(Actor, id, support, ct);
    }

    [HttpPost("{id:guid}/elections")]
    [EnableRateLimiting("financial")]
    public Task<V1ElectionDto> OpenElection(Guid id, V1OpenElectionRequest request, CancellationToken ct) => funds.OpenElectionAsync(Actor, id, request, ct);

    [HttpGet("elections/{id:guid}")]
    public Task<V1ElectionView> Election(Guid id, CancellationToken ct) => funds.ElectionAsync(Actor, id, ct);

    [HttpPost("elections/{id:guid}/candidates")]
    [EnableRateLimiting("financial")]
    public Task<V1ElectionDto> Candidate(Guid id, V1PinRequest request, CancellationToken ct) => funds.RegisterCandidateAsync(Actor, id, request.SecurityPin, ct);

    [HttpPost("elections/{id:guid}/ballots")]
    [EnableRateLimiting("financial")]
    public Task<V1BallotReceipt> Ballot(Guid id, V1BallotRequest request, CancellationToken ct) => funds.CastBallotAsync(Actor, id, request, ct);

    [HttpPost("elections/{id:guid}/advance")]
    [EnableRateLimiting("financial")]
    public Task<V1ElectionDto> AdvanceElection(Guid id, CancellationToken ct) => funds.AdvanceElectionAsync(Actor, id, ct);

    [HttpPost("elections/{id:guid}/accept")]
    [EnableRateLimiting("financial")]
    public Task<V1ElectionDto> AcceptElection(Guid id, V1PinRequest request, CancellationToken ct) => funds.AcceptElectedOwnershipAsync(Actor, id, request.SecurityPin, ct);

    [HttpGet("{id:guid}/contribution-allocation")]
    public Task<V1ContributionAllocationDto> Allocation(Guid id, CancellationToken ct) => funds.ContributionAllocationAsync(Actor, id, ct);

    [HttpGet("{id:guid}/finance")]
    public Task<V1FinanceDto> Finance(Guid id, CancellationToken ct) => funds.FinanceAsync(Actor, id, ct);

    [HttpPost("{id:guid}/obligations/confirm")]
    [EnableRateLimiting("financial")]
    public Task<V1FinanceDto> Confirm(Guid id, V1ConfirmObligationsRequest request, CancellationToken ct) => funds.ConfirmObligationsAsync(Actor, id, request, ct);

    [HttpPost("{id:guid}/contributions")]
    [EnableRateLimiting("financial")]
    public Task<V1ContributionDto> Contribute(Guid id, V1ContributeRequest request, CancellationToken ct) => funds.ContributeAsync(Actor, id, request, ct);

    [HttpGet("{id:guid}/contributions")]
    public Task<IReadOnlyList<V1ContributionDto>> Contributions(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        funds.ContributionsAsync(Actor, id, page, pageSize, ct);

    [HttpGet("{id:guid}/activities")]
    public Task<IReadOnlyList<V1ActivityDto>> Activities(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        funds.ActivitiesAsync(Actor, id, page, pageSize, ct);

    [HttpPost("{id:guid}/invitations")]
    public Task<V1InvitationDto> Invite(Guid id, V1InviteRequest request, CancellationToken ct) => funds.InviteAsync(Actor, id, request.UserId, ct);

    [HttpGet("invitations")]
    public Task<IReadOnlyList<V1InvitationDto>> Invitations([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        funds.InvitationsAsync(Actor, ct, page, pageSize);

    [HttpGet("{id:guid}/invitation-proposals")]
    public Task<IReadOnlyList<V1InvitationDto>> Proposals(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        funds.InvitationProposalsAsync(Actor, id, page, pageSize, ct);

    [HttpPost("invitations/{id:guid}/review")]
    public Task<V1InvitationDto> Review(Guid id, V1InvitationReviewRequest request, CancellationToken ct) =>
        funds.ReviewInvitationAsync(Actor, id, request.Approved, ct);

    [HttpPost("invitations/{id:guid}/decline")]
    public Task<V1InvitationDto> Decline(Guid id, CancellationToken ct) => funds.ResolveInvitationAsync(Actor, id, false, ct);

    [HttpDelete("invitations/{id:guid}")]
    public Task<V1InvitationDto> Cancel(Guid id, CancellationToken ct) => funds.ResolveInvitationAsync(Actor, id, true, ct);

    [HttpPost("invitations/{id:guid}/accept")]
    public Task<V1FundDto> Accept(Guid id, CancellationToken ct) => funds.AcceptAsync(Actor, id, ct);

    [HttpPut("{id:guid}/members/{userId:guid}/spending-permission")]
    [EnableRateLimiting("financial")]
    public async Task<IActionResult> Permission(Guid id, Guid userId, V1PermissionRequest request, CancellationToken ct)
    {
        await funds.SetSpendingPermissionAsync(Actor, id, userId, request.Enabled, request.SecurityPin, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/ownership-offers")]
    [EnableRateLimiting("financial")]
    public Task<V1OwnershipOfferDto> Offer(Guid id, V1OwnershipRequest request, CancellationToken ct) =>
        funds.OfferOwnershipAsync(Actor, id, request.UserId, request.SecurityPin, ct);

    [HttpPost("ownership-offers/{id:guid}/accept")]
    [EnableRateLimiting("financial")]
    public async Task<IActionResult> AcceptOffer(Guid id, V1PinRequest request, CancellationToken ct)
    {
        await funds.AcceptOwnershipAsync(Actor, id, request.SecurityPin, ct);
        return NoContent();
    }

    [HttpGet("ownership-offers")]
    public Task<IReadOnlyList<V1OwnershipOfferDto>> OwnershipOffers(CancellationToken ct) => funds.OwnershipOffersAsync(Actor, ct);
}
