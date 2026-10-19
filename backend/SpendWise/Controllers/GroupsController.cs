using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Groups.Commands.AcceptGroupInvite;
using SpendWise.Application.Features.Groups.Commands.CreateGroup;
using SpendWise.Application.Features.Groups.Commands.InviteGroupMember;
using SpendWise.Application.Features.Groups.Commands.RemoveGroupMember;
using SpendWise.Application.Features.Groups.Commands.TransferGroupOwnership;
using SpendWise.Application.Features.Groups.Commands.UpdateGroup;
using SpendWise.Application.Features.Groups.Queries.GetGroupDetails;
using SpendWise.Application.Features.Groups.Queries.GetGroups;
using SpendWise.Application.Features.SharedFunds.Queries.GetSharedFund;
using SpendWise.Application.Groups;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize]
[Route("api/groups")]
public sealed class GroupsController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<GroupDto>> List(CancellationToken ct) =>
        mediator.Send(new GetGroupsQuery(UserId()), ct);

    [HttpGet("{groupId:guid}")]
    public Task<GroupDto> Get(Guid groupId, CancellationToken ct) =>
        mediator.Send(new GetGroupDetailsQuery(UserId(), groupId), ct);

    [HttpPost]
    public Task<GroupDto> Create(CreateGroupRequest request, CancellationToken ct) =>
        mediator.Send(new CreateGroupCommand(UserId(), request), ct);

    [HttpPut("{groupId:guid}")]
    public Task<GroupDto> Update(Guid groupId, UpdateGroupRequest request, CancellationToken ct) =>
        mediator.Send(new UpdateGroupCommand(UserId(), groupId, request), ct);

    [HttpPost("{groupId:guid}/invites")]
    public Task<GroupInviteDto> Invite(Guid groupId, InviteGroupRequest request, CancellationToken ct) =>
        mediator.Send(new InviteGroupMemberCommand(UserId(), groupId, request), ct);

    [HttpGet("invites")]
    public Task<IReadOnlyList<GroupInviteDto>> Invitations(CancellationToken ct) =>
        mediator.Send(new GetGroupInvitationsQuery(UserId()), ct);

    [HttpPost("invites/{inviteId:guid}/accept")]
    public Task<GroupDto> Accept(Guid inviteId, [FromQuery] string token, CancellationToken ct) =>
        mediator.Send(new AcceptGroupInviteCommand(UserId(), inviteId, token), ct);

    [HttpPost("invites/{inviteId:guid}/reject")]
    public Task<GroupInviteDto> Reject(Guid inviteId, CancellationToken ct) =>
        mediator.Send(new RejectGroupInviteCommand(UserId(), inviteId), ct);

    [HttpDelete("{groupId:guid}/members/{memberUserId:guid}")]
    public async Task<IActionResult> Remove(Guid groupId, Guid memberUserId, CancellationToken ct)
    {
        await mediator.Send(new RemoveGroupMemberCommand(UserId(), groupId, memberUserId), ct);
        return NoContent();
    }

    [HttpPost("{groupId:guid}/ownership/{memberUserId:guid}")]
    public async Task<IActionResult> TransferOwnership(Guid groupId, Guid memberUserId, CancellationToken ct)
    {
        await mediator.Send(new TransferGroupOwnershipCommand(UserId(), groupId, memberUserId), ct);
        return NoContent();
    }

    [HttpPut("{groupId:guid}/receiving-bank")]
    public Task<GroupReceivingBankDto> SetReceivingBank(Guid groupId, GroupReceivingBankRequest request, CancellationToken ct) =>
        mediator.Send(new SetGroupReceivingBankCommand(UserId(), groupId, request), ct);

    [HttpGet("{groupId:guid}/fund")]
    public Task<SharedFundDto> Fund(Guid groupId, CancellationToken ct) =>
        mediator.Send(new GetSharedFundQuery(UserId(), groupId), ct);

    [HttpGet("{groupId:guid}/fund/transactions")]
    public Task<IReadOnlyList<FundTransactionDto>> FundTransactions(Guid groupId, CancellationToken ct) =>
        mediator.Send(new GetFundTransactionsQuery(UserId(), groupId), ct);

    [HttpPost("{groupId:guid}/fund/contributions")]
    [EnableRateLimiting("financial")]
    public Task<GroupContributionDto> Contribute(Guid groupId, CreateFundContributionPaymentRequest request, CancellationToken ct) =>
        mediator.Send(new CreateFundContributionPaymentCommand(UserId(), groupId, request), ct);

    [HttpGet("{groupId:guid}/fund-requests")]
    public Task<IReadOnlyList<FundRequestDto>> FundRequests(Guid groupId, CancellationToken ct) =>
        mediator.Send(new GetFundRequestsQuery(UserId(), groupId), ct);

    [HttpPost("{groupId:guid}/fund-requests")]
    public Task<FundRequestDto> CreateFundRequest(Guid groupId, CreateFundRequestRequest request, CancellationToken ct) =>
        mediator.Send(new CreateFundRequestCommand(UserId(), groupId, request), ct);

    [HttpPost("{groupId:guid}/fund-requests/{requestId:guid}/pay")]
    [EnableRateLimiting("financial")]
    public Task<GroupContributionDto> PayFundRequest(Guid groupId, Guid requestId, PayFundRequestRequest request, CancellationToken ct) =>
        mediator.Send(new PayFundRequestCommand(UserId(), groupId, requestId, request), ct);

    [HttpGet("{groupId:guid}/fund-rounds")]
    public Task<IReadOnlyList<FundRoundDto>> FundRounds(Guid groupId, CancellationToken ct) =>
        mediator.Send(new GetFundRoundsQuery(UserId(), groupId), ct);

    [HttpPost("{groupId:guid}/fund-rounds")]
    public Task<FundRoundDto> CreateFundRound(Guid groupId, CreateFundRoundRequest request, CancellationToken ct) =>
        mediator.Send(new CreateFundRoundCommand(UserId(), groupId, request), ct);

    [HttpPost("{groupId:guid}/fund-rounds/{roundId:guid}/pay")]
    [EnableRateLimiting("financial")]
    public Task<GroupContributionDto> PayFundRound(Guid groupId, Guid roundId, PayFundRoundRequest request, CancellationToken ct) =>
        mediator.Send(new PayFundRoundCommand(UserId(), groupId, roundId, request), ct);

    [HttpGet("{groupId:guid}/expenses")]
    public Task<IReadOnlyList<GroupExpenseDto>> Expenses(Guid groupId, CancellationToken ct) =>
        mediator.Send(new GetCurrentGroupExpensesQuery(UserId(), groupId), ct);

    [HttpPost("{groupId:guid}/expenses")]
    [EnableRateLimiting("financial")]
    public Task<GroupExpenseDto> CreateExpense(Guid groupId, CreateGroupExpenseRequest request, CancellationToken ct) =>
        mediator.Send(new CreateCurrentGroupExpenseCommand(UserId(), groupId, request), ct);

    [HttpGet("{groupId:guid}/activities")]
    public Task<IReadOnlyList<GroupActivityDto>> Activities(Guid groupId, CancellationToken ct) =>
        mediator.Send(new GetGroupActivitiesQuery(UserId(), groupId), ct);

    [HttpGet("{groupId:guid}/deletion-request")]
    public Task<GroupDeletionRequestDto?> Deletion(Guid groupId, CancellationToken ct) =>
        mediator.Send(new GetGroupDeletionRequestQuery(UserId(), groupId), ct);

    [HttpPost("{groupId:guid}/deletion-requests")]
    public Task<GroupDeletionRequestDto> RequestDeletion(Guid groupId, CancellationToken ct) =>
        mediator.Send(new CreateGroupDeletionRequestCommand(UserId(), groupId), ct);

    [HttpPost("{groupId:guid}/deletion-requests/{requestId:guid}/vote")]
    public Task<GroupDeletionRequestDto> VoteDeletion(Guid groupId, Guid requestId, GroupDeletionVoteRequest request, CancellationToken ct) =>
        mediator.Send(new VoteGroupDeletionCommand(UserId(), groupId, requestId, request), ct);

    [HttpPost("{groupId:guid}/deletion-requests/{requestId:guid}/cancel")]
    public Task<GroupDeletionRequestDto> CancelDeletion(Guid groupId, Guid requestId, CancellationToken ct) =>
        mediator.Send(new CancelGroupDeletionCommand(UserId(), groupId, requestId), ct);

    [HttpPost("{groupId:guid}/deletion-requests/{requestId:guid}/refunds/{refundId:guid}/payment")]
    [EnableRateLimiting("financial")]
    public Task<GroupRefundDto> CreateRefundPayment(Guid groupId, Guid requestId, Guid refundId,
        CreateGroupRefundPaymentRequest request, CancellationToken ct) =>
        mediator.Send(new CreateGroupRefundPaymentCommand(UserId(), groupId, requestId, refundId, request), ct);

    [HttpPost("{groupId:guid}/deletion-requests/{requestId:guid}/finalize")]
    [EnableRateLimiting("financial")]
    public Task<GroupDeletionRequestDto> FinalizeDeletion(Guid groupId, Guid requestId,
        FinalizeGroupDeletionRequest request, CancellationToken ct) =>
        mediator.Send(new FinalizeGroupDeletionCommand(UserId(), groupId, requestId, request), ct);

    private Guid UserId() => currentUser.UserId ?? throw new UnauthorizedAccessException();
}
