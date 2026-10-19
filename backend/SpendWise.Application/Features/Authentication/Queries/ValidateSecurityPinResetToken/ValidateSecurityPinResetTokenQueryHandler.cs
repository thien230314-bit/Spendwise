using MediatR;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Queries.ValidateSecurityPinResetToken;

public sealed class ValidateSecurityPinResetTokenQueryHandler(
    SecurityPinResetSupport resets)
    : IRequestHandler<ValidateSecurityPinResetTokenQuery, SecurityPinResetTokenStatusDto>
{
    public async Task<SecurityPinResetTokenStatusDto> Handle(
        ValidateSecurityPinResetTokenQuery query,
        CancellationToken cancellationToken)
    {
        // Chỉ xác nhận hiệu lực, không trả thông tin tài khoản sở hữu token.
        try
        {
            var token = await resets.GetValidAsync(query.Request.Token, cancellationToken);
            return new SecurityPinResetTokenStatusDto(true, token.ExpiresAt);
        }
        catch (AppException)
        {
            return new SecurityPinResetTokenStatusDto(false, default);
        }
    }
}
