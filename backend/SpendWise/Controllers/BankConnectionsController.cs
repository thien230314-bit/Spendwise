using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.BankConnections.Commands.CreateBankLink;
using SpendWise.Application.Features.BankConnections.Commands.ReactivateBankConnection;
using SpendWise.Application.Features.BankConnections.Commands.SyncBankConnection;
using SpendWise.Application.Features.BankConnections.Commands.UnlinkBankConnection;
using SpendWise.Application.Features.BankConnections.Queries.GetBankConnectionById;
using SpendWise.Application.Features.BankConnections.Queries.GetBankConnections;
using SpendWise.Application.Features.BankConnections;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize]
[Route("api/bank-connections")]
public sealed class BankConnectionsController(
    IMediator mediator,
    ICurrentUser currentUser,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("link")]
    [EnableRateLimiting("financial")]
    public async Task<BankLinkSessionDto> Link(
        CreateBankLinkRequest request,
        CancellationToken cancellationToken)
    {
        // UserId lấy từ access token; frontend chỉ gửi redirect và ngôn ngữ.
        return await mediator.Send(new CreateBankLinkCommand(UserId(), request), cancellationToken);
    }

    [HttpGet]
    public async Task<IReadOnlyList<BankConnectionDto>> List(CancellationToken cancellationToken)
    {
        return await mediator.Send(new GetBankConnectionsQuery(UserId()), cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<BankConnectionDto> Get(Guid id, CancellationToken cancellationToken)
    {
        return await mediator.Send(new GetBankConnectionByIdQuery(UserId(), id), cancellationToken);
    }

    [HttpPost("{id:guid}/unlink")]
    [EnableRateLimiting("financial")]
    public async Task<BankLinkSessionDto> Unlink(
        Guid id,
        CreateBankLinkRequest request,
        CancellationToken cancellationToken)
    {
        // Provider mới là nguồn có thẩm quyền xác nhận hủy liên kết qua webhook.
        return await mediator.Send(new UnlinkBankConnectionCommand(
            UserId(), id, request, environment.IsDevelopment()), cancellationToken);
    }

    [HttpPost("{id:guid}/reactivate")]
    [EnableRateLimiting("financial")]
    public async Task<BankLinkSessionDto> Reactivate(
        Guid id,
        CreateBankLinkRequest request,
        CancellationToken cancellationToken)
    {
        return await mediator.Send(new ReactivateBankConnectionCommand(UserId(), id, request), cancellationToken);
    }

    [HttpPost("{id:guid}/sync")]
    [EnableRateLimiting("financial")]
    public async Task<BankSyncDto> Sync(Guid id, CancellationToken cancellationToken)
    {
        return await mediator.Send(new SyncBankConnectionCommand(UserId(), id), cancellationToken);
    }

    [HttpPost("test-mode/link-challenges")]
    [EnableRateLimiting("financial")]
    public Task<TestModeBankLinkChallengeDto> StartTestModeLink(CancellationToken cancellationToken) =>
        mediator.Send(new StartTestModeBankLinkCommand(environment.IsDevelopment(), UserId()), cancellationToken);

    [HttpPost("test-mode/link-challenges/{challengeId:guid}/confirm")]
    [EnableRateLimiting("financial")]
    public Task<BankConnectionDto> ConfirmTestModeLink(Guid challengeId, CancellationToken cancellationToken) =>
        mediator.Send(new ConfirmTestModeBankLinkCommand(
            environment.IsDevelopment(), UserId(), challengeId), cancellationToken);


    [HttpPost("test-mode/wallet-top-ups")]
    [EnableRateLimiting("financial")]
    public Task<WalletTopUpDto> CreateTestModeTopUp(
        CreateWalletTopUpRequest request,
        CancellationToken cancellationToken) => mediator.Send(
            new CreateTestModeWalletTopUpCommand(environment.IsDevelopment(), UserId(), request), cancellationToken);

    [HttpPost("test-mode/wallet-top-ups/{topUpId:guid}/confirm")]
    [EnableRateLimiting("financial")]
    public Task<WalletTopUpDto> ConfirmTestModeTopUp(Guid topUpId, CancellationToken cancellationToken) =>
        mediator.Send(new ConfirmTestModeWalletTopUpCommand(
            environment.IsDevelopment(), UserId(), topUpId), cancellationToken);

    [HttpGet("test-mode/linked-accounts")]
    public Task<IReadOnlyList<TestModeLinkedAccountDto>> GetTestModeLinkedAccounts(
        CancellationToken cancellationToken) => mediator.Send(
            new GetTestModeLinkedAccountsQuery(environment.IsDevelopment(), UserId()), cancellationToken);

    [HttpGet("test-mode/wallet-top-ups")]
    public Task<IReadOnlyList<WalletTopUpDto>> GetTestModeWalletTopUps(
        CancellationToken cancellationToken) => mediator.Send(
            new GetTestModeWalletTopUpsQuery(environment.IsDevelopment(), UserId()), cancellationToken);

    [HttpPost("test-mode/wallet-withdrawals")]
    [EnableRateLimiting("financial")]
    public Task<WalletWithdrawalDto> CreateTestModeWithdrawal(
        CreateWalletWithdrawalRequest request,
        CancellationToken cancellationToken) => mediator.Send(
            new CreateWalletWithdrawalCommand(environment.IsDevelopment(), UserId(), request), cancellationToken);

    [HttpGet("test-mode/wallet-withdrawals")]
    public Task<IReadOnlyList<WalletWithdrawalDto>> GetTestModeWithdrawals(
        CancellationToken cancellationToken) => mediator.Send(
            new GetWalletWithdrawalsQuery(environment.IsDevelopment(), UserId()), cancellationToken);

    [HttpGet("test-mode/wallet-withdrawals/{id:guid}")]
    public Task<WalletWithdrawalDto> GetTestModeWithdrawalById(
        Guid id,
        CancellationToken cancellationToken) => mediator.Send(
            new GetWalletWithdrawalByIdQuery(environment.IsDevelopment(), UserId(), id), cancellationToken);

    [HttpPost("test-mode/wallet-withdrawals/{id:guid}/confirm")]
    [EnableRateLimiting("financial")]
    public Task<WalletWithdrawalDto> ConfirmTestModeWithdrawal(
        Guid id,
        CancellationToken cancellationToken) => mediator.Send(
            new ConfirmTestModeWalletWithdrawalCommand(environment.IsDevelopment(), UserId(), id), cancellationToken);

    [HttpPost("test-mode/wallet-withdrawals/{id:guid}/cancel")]
    [EnableRateLimiting("financial")]
    public Task<WalletWithdrawalDto> CancelTestModeWithdrawal(
        Guid id,
        CancellationToken cancellationToken) => mediator.Send(
            new CancelTestModeWalletWithdrawalCommand(environment.IsDevelopment(), UserId(), id), cancellationToken);

    private Guid UserId() => currentUser.UserId ?? throw new UnauthorizedAccessException();
}
