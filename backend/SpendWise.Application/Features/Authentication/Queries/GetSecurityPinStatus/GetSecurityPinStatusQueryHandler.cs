using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Queries.GetSecurityPinStatus;

public sealed class GetSecurityPinStatusQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetSecurityPinStatusQuery, SecurityPinStatusDto>
{
    public async Task<SecurityPinStatusDto> Handle(
        GetSecurityPinStatusQuery query,
        CancellationToken cancellationToken)
    {
        var pinHash = await db.Users
            .Where(item => item.Id == query.UserId)
            .Select(item => item.SecurityPinHash)
            .SingleOrDefaultAsync(cancellationToken);
        if (pinHash is null)
        {
            // User hợp lệ có thể chưa có PIN; phân biệt bằng kiểm tra tồn tại khi hash null.
            var exists = await db.Users.AnyAsync(item => item.Id == query.UserId, cancellationToken);
            if (!exists) throw new NotFoundException("user_not_found", "User not found.");
        }

        return new SecurityPinStatusDto(!string.IsNullOrWhiteSpace(pinHash));
    }
}
