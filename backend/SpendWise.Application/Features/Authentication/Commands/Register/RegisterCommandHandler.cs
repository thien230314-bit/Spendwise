using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Identity;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.Register;

// Tạo tài khoản, ví mặc định và phiên đăng nhập đầu tiên trong một transaction.
public sealed class RegisterCommandHandler(
    ISpendWiseDbContext db,
    IPasswordService passwords,
    EmailVerificationSupport emailVerification) : IRequestHandler<RegisterCommand, EmailVerificationPendingResponse>
{
    public async Task<EmailVerificationPendingResponse> Handle(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        // Kiểm tra dữ liệu tài khoản trước khi tạo người dùng.
        InputBoundary.Registration(
            command.Request.FullName,
            command.Request.Email,
            command.Request.Phone);
        if (!string.Equals(
                command.Request.Password,
                command.Request.ConfirmPassword,
                StringComparison.Ordinal))
        {
            throw new AppException(
                "password_mismatch",
                "Password and confirmation password do not match.");
        }
        AuthenticationSupport.ValidatePassword(command.Request.Password);
        var normalizedEmail = InputBoundary.NormalizeEmail(command.Request.Email);
        var normalizedPhone = InputBoundary.NormalizePhone(command.Request.Phone);
        // SpendID vẫn tồn tại nội bộ nhưng được backend tạo, không còn tin dữ liệu client.
        var spendId = $"SW{Guid.NewGuid():N}";
        var normalizedSpendId = spendId.ToUpperInvariant();
        // Toàn bộ tài khoản bắt buộc phải được tạo hoặc rollback cùng nhau.
        return await db.ExecuteInTransactionAsync(async token =>
        {
            var emailExists = await db.Users.AnyAsync(
                user => user.NormalizedEmail == normalizedEmail,
                token);
            if (emailExists)
            {
                throw new ConflictException(
                    "email_exists",
                    "Email is already registered.");
            }

            var phoneExists = await db.Users.AnyAsync(
                user => user.Phone == normalizedPhone,
                token);
            if (phoneExists)
            {
                throw new ConflictException(
                    "phone_exists",
                    "Phone is already registered.");
            }

            var user = new User
            {
                FullName = command.Request.FullName.Trim(),
                Email = command.Request.Email.Trim(),
                NormalizedEmail = normalizedEmail,
                SpendId = spendId,
                NormalizedSpendId = normalizedSpendId,
                Phone = normalizedPhone,
                // Chỉ lưu mật khẩu đã hash, không lưu plaintext.
                PasswordHash = passwords.Hash(command.Request.Password),
                // Tài khoản đăng ký qua API phải xác minh email trước khi nhận challenge PIN.
                EmailVerifiedAt = null
            };
            db.Users.Add(user);

            // Ví ban đầu và LedgerAccount đều có số dư 0; không tạo tiền giả.
            var wallet = WalletFactory.Create(
                user.Id,
                "Ví chính",
                WalletType.Main,
                true);
            db.Wallets.Add(wallet);

            // E01 được xếp cùng transaction tạo tài khoản; lỗi gửi sau đó không rollback tài khoản.
            return await emailVerification.IssueRegistrationOtpAsync(
                user,
                command.IpAddress,
                token);
        }, cancellationToken);
    }
}
