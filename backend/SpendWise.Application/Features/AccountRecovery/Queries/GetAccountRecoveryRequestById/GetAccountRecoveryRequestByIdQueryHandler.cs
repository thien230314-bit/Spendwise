using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.AccountRecovery;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.AccountRecovery.Queries.GetAccountRecoveryRequestById;

public sealed class GetAccountRecoveryRequestByIdQueryHandler(
    ISpendWiseDbContext db)
    : IRequestHandler<GetAccountRecoveryRequestByIdQuery, AccountRecoveryDto>
{
    public async Task<AccountRecoveryDto> Handle(
        GetAccountRecoveryRequestByIdQuery query,
        CancellationToken cancellationToken)
    {
        var request = await db.AccountRecoveryRequests.AsNoTracking()
            .Include(item => item.Reviews)
            .SingleOrDefaultAsync(item => item.Id == query.RequestId, cancellationToken)
            ?? throw new NotFoundException("recovery_request_not_found", "Account recovery request not found.");
        return AccountRecoveryMappings.ToDto(request);
    }
}
