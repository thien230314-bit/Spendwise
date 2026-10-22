using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Friends;
using SpendWise.Application.Finance;
using SpendWise.Application.Social;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize]
[Route("api/friends")]
public sealed class FriendsController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("search")]
    public Task<FriendContactDto> Search([FromQuery] string contact, CancellationToken ct, [FromQuery] string? kind = null) =>
        mediator.Send(new SearchFriendContactQuery(UserId(), contact, kind), ct);

    [HttpGet("discover")]
    [EnableRateLimiting("userDiscovery")]
    public Task<PagedResult<UserDiscoveryDto>> Discover(
        [FromQuery] string query,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        mediator.Send(new DiscoverUsersQuery(UserId(), query, page, pageSize), ct);

    [HttpGet]
    public Task<IReadOnlyList<FriendDto>> List(CancellationToken ct) =>
        mediator.Send(new GetFriendsQuery(UserId()), ct);

    [HttpGet("requests/incoming")]
    public Task<IReadOnlyList<FriendRequestDto>> Incoming(CancellationToken ct) =>
        mediator.Send(new GetIncomingFriendRequestsQuery(UserId()), ct);

    [HttpGet("requests/outgoing")]
    public Task<IReadOnlyList<FriendRequestDto>> Outgoing(CancellationToken ct) =>
        mediator.Send(new GetOutgoingFriendRequestsQuery(UserId()), ct);

    [HttpPost("requests")]
    public Task<FriendRequestDto> Send(SendFriendRequest request, CancellationToken ct) =>
        mediator.Send(new SendFriendRequestCommand(UserId(), request), ct);

    [HttpDelete("requests/{requestId:guid}")]
    public Task<FriendRequestDto> Cancel(Guid requestId, CancellationToken ct) =>
        mediator.Send(new CancelFriendRequestCommand(UserId(), requestId), ct);

    [HttpPost("requests/{requestId:guid}/accept")]
    public Task<FriendDto> Accept(Guid requestId, CancellationToken ct) =>
        mediator.Send(new AcceptFriendRequestCommand(UserId(), requestId), ct);

    [HttpPost("requests/{requestId:guid}/reject")]
    public Task<FriendRequestDto> Reject(Guid requestId, CancellationToken ct) =>
        mediator.Send(new RejectFriendRequestCommand(UserId(), requestId), ct);

    [HttpDelete("{friendUserId:guid}")]
    public async Task<IActionResult> Unfriend(Guid friendUserId, CancellationToken ct)
    {
        await mediator.Send(new UnfriendCommand(UserId(), friendUserId), ct);
        return NoContent();
    }

    [HttpPatch("{friendUserId:guid}/preference")]
    public Task<FriendDto> Preference(Guid friendUserId, UpdateFriendPreferenceRequest request, CancellationToken ct) =>
        mediator.Send(new UpdateFriendPreferenceCommand(UserId(), friendUserId, request), ct);

    private Guid UserId() => currentUser.UserId ?? throw new UnauthorizedAccessException();
}
