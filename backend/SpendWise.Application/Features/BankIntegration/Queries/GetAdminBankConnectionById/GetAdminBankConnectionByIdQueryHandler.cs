using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetAdminBankConnectionById;

public sealed class GetAdminBankConnectionByIdQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetAdminBankConnectionByIdQuery, AdminBankConnectionDto>
{
    public async Task<AdminBankConnectionDto> Handle(
        GetAdminBankConnectionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var connection = await db.BankConnections.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == query.BankConnectionId,
            cancellationToken) ?? throw BankingErrors.ConnectionNotFound();
        return BankingMappings.ToAdminDto(connection);
    }
}
