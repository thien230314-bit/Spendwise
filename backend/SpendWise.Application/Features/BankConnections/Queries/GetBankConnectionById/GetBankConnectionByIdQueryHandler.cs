using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Banking;

namespace SpendWise.Application.Features.BankConnections.Queries.GetBankConnectionById;

public sealed class GetBankConnectionByIdQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetBankConnectionByIdQuery, BankConnectionDto>
{
    public async Task<BankConnectionDto> Handle(
        GetBankConnectionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var connection = await db.BankConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == query.BankConnectionId && item.UserId == query.UserId,
                cancellationToken) ?? throw BankingErrors.ConnectionNotFound();
        return BankingMappings.ToUserDto(connection);
    }
}
