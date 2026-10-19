using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SpendWise.Application.Features.Authentication.Commands.ForgotPassword;
using SpendWise.Application.Features.Authentication.Commands.ResetPassword;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.AdminPasswordRecovery;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var stage = "configuration";
        try
        {
            if (args.SequenceEqual(["--self-test-api"]))
            {
                await ApiRecovery.SelfTestAsync();
                return 0;
            }
            if (args.SequenceEqual(["--api-reset"]))
            {
                stage = "private Reset Password API";
                return await ResetViaApiAsync();
            }
            if (args.Length > 1 || (args.Length == 1 && args[0] is not ("--check" or "--api-forgot-dispatch")))
                throw new InvalidOperationException("No secret or target arguments are accepted.");
            const string root = @"E:\source\SpendWise-final-merged";
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(root, "backend", "SpendWise", "appsettings.json"), optional: false)
                .AddUserSecrets("SpendWise-Development", reloadOnChange: false)
                .Build();
            RecoveryServices.ValidateConfiguration(configuration);
            await using var provider = RecoveryServices.Create(configuration)
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            if (provider.GetServices<IHostedService>().Any())
                throw new InvalidOperationException("Hosted services are forbidden.");
            await using (var check = provider.CreateAsyncScope())
            {
                _ = check.ServiceProvider.GetRequiredService<ForgotPasswordCommandHandler>();
                _ = check.ServiceProvider.GetRequiredService<ResetPasswordCommandHandler>();
            }
            if (args.SequenceEqual(["--api-forgot-dispatch"]))
            {
                stage = "Forgot Password API / targeted delivery; do not automatically retry";
                using var apiDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                await ApiRecovery.IssueAndDeliverAsync(provider, apiDeadline.Token);
                return 0;
            }
            if (args.SequenceEqual(["--check"]))
            {
                Console.WriteLine("Configuration/wiring PASS. No SQL connection, SMTP, host, migration, seeding or workers started.");
                return 0;
            }
            if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
                throw new InvalidOperationException("Use a private interactive console without redirection.");
            Console.WriteLine("Use only your private CMD: no recording, transcript, screen sharing or debugger.");
            Console.WriteLine("Stop API/background workers before continuing. This tool does not stop them.");
            Console.WriteLine("Type PRIVATE to confirm private console and no other recovery/outbox writers:");
            if (Console.ReadLine() != "PRIVATE") return 1;
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            var ct = deadline.Token;
            Guid id;
            stage = "read-only target verification";
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
                await db.Database.OpenConnectionAsync(ct);
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME()";
                await using (var reader = await command.ExecuteReaderAsync(ct))
                    if (!await reader.ReadAsync(ct) || reader.GetString(0) != RecoveryServices.Server
                        || reader.GetString(1) != RecoveryServices.Database)
                        throw new InvalidOperationException("Connected target mismatch.");
                id = await scope.ServiceProvider.GetRequiredService<RecoverySession>().IdentifyAsync(ct);
            }
            Console.WriteLine($"Verified existing SystemAdmin: {RecoverySession.Email}; UserId={id}");
            Console.WriteLine($"Type SEND {id} to authorize one OTP email (this writes recovery records):");
            if (Console.ReadLine() != $"SEND {id}") return 1;
            RecoveryTicket ticket;
            stage = "OTP issuance";
            await using (var scope = provider.CreateAsyncScope())
                ticket = await scope.ServiceProvider.GetRequiredService<RecoverySession>().IssueAsync(id, ct);
            stage = "targeted SMTP delivery — do not automatically retry an uncertain send";
            await using (var scope = provider.CreateAsyncScope())
            using (var sendDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                sendDeadline.CancelAfter(TimeSpan.FromSeconds(45));
                if (!await scope.ServiceProvider.GetRequiredService<RecoverySession>().DeliverAsync(ticket, sendDeadline.Token))
                    throw new InvalidOperationException("Email not confirmed sent. Stop; do not automatically retry.");
            }
            Console.WriteLine("SMTP send completed for this recovery email. Check Gmail (including Spam).");
            var otp = ReadHidden("OTP (8 digits): ", 8);
            var password = ReadHidden("New password (8-128 chars; upper/lower/digit): ", 128);
            var confirmation = ReadHidden("Confirm new password: ", 128);
            if (!string.Equals(password, confirmation, StringComparison.Ordinal))
                throw new InvalidOperationException("Password confirmation differs.");
            Console.WriteLine("Type RESET to authorize password reset and session revocation; PIN is unchanged:");
            if (Console.ReadLine() != "RESET") return 1;
            stage = "password reset/commit — reconcile before retry if outcome is uncertain";
            await using (var scope = provider.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<RecoverySession>().ResetAsync(ticket, otp, password, ct);
            otp = password = confirmation = string.Empty;
            Console.WriteLine("PASSWORD RESET COMPLETED via existing handler. Old sessions revoked; PIN unchanged.");
            Console.WriteLine("Sign in to Admin Web with the new email/password and your existing PIN.");
            Console.WriteLine("The handler's success-notification email remains queued; this CLI sends only the OTP.");
            return 0;
        }
        catch
        {
            // Exception messages/provider details may contain credentials or request contents.
            Console.Error.WriteLine($"STOP at {stage}. No automatic resend/reset retry. Review locally without sharing secrets.");
            return 1;
        }
    }

    private static async Task<int> ResetViaApiAsync()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
            throw new InvalidOperationException("Use a private interactive CMD without redirection.");
        Console.WriteLine("Private CMD only: no transcript, recording, debugger or screen sharing. Keep AdminE2E API running.");
        Console.WriteLine("This mode sends no email and creates no new OTP. It calls only the real Reset Password API.");
        var otp = ReadHidden("Gmail OTP (8 digits): ", 8);
        if (otp.Length != 8 || otp.Any(x => !char.IsAsciiDigit(x)))
            throw new InvalidOperationException("OTP must contain eight digits.");
        var password = ReadHidden("New password (8-128 chars; upper/lower/digit): ", 128);
        var confirm = ReadHidden("Confirm password: ", 128);
        if (!string.Equals(password, confirm, StringComparison.Ordinal))
            throw new InvalidOperationException("Password confirmation differs.");
        SpendWise.Application.Features.Authentication.AuthenticationSupport.ValidatePassword(password);
        Console.WriteLine("Type RESET to authorize one API request; PIN is unchanged:");
        if (Console.ReadLine() != "RESET") return 1;
        using var api = new RecoveryApiClient();
        var status = await api.ResetAsync(otp, password, default);
        otp = password = confirm = string.Empty;
        Console.WriteLine($"Reset Password API HTTP {(int)status}.");
        if (status != System.Net.HttpStatusCode.NoContent)
        {
            Console.WriteLine("STOP. No automatic retry. Do not share OTP/password or raw request/response bodies.");
            return 1;
        }
        Console.WriteLine("RESET SUCCESS (HTTP 204). Sign in at http://localhost:3000/login with the new password and existing PIN.");
        return 0;
    }

    private static string ReadHidden(string prompt, int maximum)
    {
        Console.Write(prompt);
        var buffer = new char[maximum];
        var length = 0;
        try
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Escape) throw new OperationCanceledException();
                if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return new string(buffer, 0, length); }
                if (key.Key == ConsoleKey.Backspace) { if (length > 0) buffer[--length] = '\0'; continue; }
                if (char.IsControl(key.KeyChar)) continue;
                if (length == maximum) throw new InvalidOperationException("Hidden input exceeds limit.");
                buffer[length++] = key.KeyChar;
            }
        }
        finally { Array.Clear(buffer); }
    }
}
