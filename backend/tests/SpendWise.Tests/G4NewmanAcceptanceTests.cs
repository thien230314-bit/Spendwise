using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.Operations;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Identity;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class G4NewmanFactAttribute : FactAttribute
{
    public G4NewmanFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SPENDWISE_RUN_G4_E2E") != "1")
            Skip = "Explicit SQL02/G4 Newman opt-in required.";
    }
}

[Trait("Category", "SqlServer")]
[Trait("Category", "G4Newman")]
public sealed class G4NewmanAcceptanceTests
{
    [G4NewmanFact]
    public async Task Full_Postman_E2E_Acceptance_Runs_On_SpendWise_E2E_G4()
    {
        var targetDb = "SpendWise_E2E_G4";
        var instance = Environment.GetEnvironmentVariable(SqlTestFixtureLifecycle.InstanceEnvironmentVariable) ?? @"DESKTOP-B5KA3LT\SQLEXPRESS02";
        var connection = SqlTestFixtureLifecycle.CreateOwnedConnectionString(targetDb, instance);

        var clock = new Task17Clock();
        var factory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:AuthPermitLimit"] = "1000",
            ["RateLimiting:FinancialPermitLimit"] = "1000",
            ["Email:DeliveryMode"] = "Pickup",
            ["PasswordReset:PickupDirectory"] = Path.Combine(Path.GetTempPath(), "spendwise-g4", "pickup"),
            ["SePayBankHub:Enabled"] = "false",
            ["Gemini:ApiKey"] = "",
            ["V1:Activity:CoverageVerified"] = "true",
            ["V1:Activity:CoverageStartsAtUtc"] = clock.UtcNow.AddDays(-1).ToString("O"),
            ["V1:Broadcast:Version"] = "G4-fixture",
            ["V1:Broadcast:MaxRecipients"] = "100",
            ["V1:Broadcast:MinSpacingSeconds"] = "60",
            ["V1:MaintenanceReminders:Version"] = "G4-fixture",
            ["V1:MaintenanceReminders:MaximumCount"] = "3",
            ["V1:MaintenanceReminders:MinimumSpacingMinutes"] = "10",
            ["V1:MaintenanceReminders:LateToleranceSeconds"] = "60"
        }, clock: clock, sqlConnection: connection);

        factory.UseKestrel(options => options.Listen(System.Net.IPAddress.Loopback, 0));
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        CoreFundSeed s;
        Guid proposalId, electionId, electionFund;
        var values = new Dictionary<string, string>();

        Func<SpendWiseDbContext> context = () => new SpendWiseDbContext(
            new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlServer(connection).Options, clock);

        await using (var db = context())
        {
            s = await CoreAcceptanceData.SeedAsync(db, ledgerOpening: true);
            s.Admin.PasswordHash = factory.Services.GetRequiredService<IPasswordService>().Hash("StrongPass1");
            db.Users.Update(s.Admin);

            if (!await db.Categories.AnyAsync(c => c.Key == "synthetic-income"))
                db.Categories.Add(new Category { Key = "synthetic-income", Name = "Synthetic income", Kind = CategoryKind.Income, IsSystem = true });
            if (!await db.Categories.AnyAsync(c => c.Key == "synthetic-expense"))
                db.Categories.Add(new Category { Key = "synthetic-expense", Name = "Synthetic expense", Kind = CategoryKind.Expense, IsSystem = true });
            await db.SaveChangesAsync();

            var members = await db.V1FundMemberships.Where(m => m.FundId == s.FundId).ToListAsync();
            var fund = await db.V1Funds.SingleAsync(x => x.Id == s.FundId);
            await db.V1Funds.Where(x => x.Id == fund.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.ActivityClosedAt, (DateTime?)clock.UtcNow.AddDays(-2)));

            var p = new V1ActivityProposal
            {
                FundId = s.FundId,
                ProposedByUserId = s.Owner.Id,
                OwnershipRevision = fund.OwnershipRevision,
                TargetVnd = 1_000_000,
                State = "ApprovedAwaitingSettlement",
                IdempotencyKey = "synthetic-newman-period-g4",
                OpenedAt = clock.UtcNow.AddDays(-1)
            };
            db.V1ActivityProposals.Add(p);
            db.V1ActivityVoters.AddRange(members.Select(m => new V1ActivityVoter
            {
                ProposalId = p.Id,
                UserId = m.UserId,
                MembershipId = m.Id,
                Support = true
            }));
            proposalId = p.Id;

            var ef = await CoreAcceptanceData.Funds(db, clock).CreateAsync(s.Owner.Id, new("Synthetic election G4", "Free", null, IdentityTestData.DefaultPin), default);
            electionFund = ef.Id;
            var em = new V1FundMembership { FundId = ef.Id, UserId = s.Member.Id, Seat = 2 };
            db.V1FundMemberships.Add(em);

            var e = new V1Election
            {
                FundId = ef.Id,
                IncumbentUserId = s.Owner.Id,
                OpenedByUserId = s.Member.Id,
                Phase = V1ElectionPhase.Nomination,
                PhaseStartedAt = clock.UtcNow.AddDays(-3),
                PolicyJson = JsonSerializer.Serialize(new V1ElectionPolicy().Snapshot()),
                InactivityEvidenceId = "synthetic-g4-fixture"
            };
            db.V1Elections.Add(e);
            db.V1ElectionVoters.Add(new() { ElectionId = e.Id, UserId = s.Member.Id, MembershipId = em.Id });
            db.V1ElectionCandidates.Add(new() { ElectionId = e.Id, UserId = s.Member.Id });
            electionId = e.Id;

            await db.SaveChangesAsync();
        }

        async Task ActorAsync(SpendWise.Domain.Identity.User actor, string key)
        {
            await using var db = context();
            var session = new UserSession { UserId = actor.Id, LastSeenAt = clock.UtcNow };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync();
            using var scope = factory.Services.CreateScope();
            values[key] = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(actor, session.Id).Token;
        }

        await ActorAsync(s.Owner, "core_owner_token");
        await ActorAsync(s.Member, "core_member_token");
        await ActorAsync(s.Admin, "core_admin_token");

        values["core_maintenance_end"] = clock.UtcNow.AddHours(2).ToString("O");
        values["baseUrl"] = factory.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.Single().TrimEnd('/');

        using (var ready = await client.GetAsync(values["baseUrl"] + "/health"))
            Assert.True(ready.IsSuccessStatusCode, "Loopback readiness failed before Newman.");

        values["admin_email"] = s.Admin.Email;
        values["admin_password"] = values["user_password"] = "StrongPass1";
        values["default_pin"] = IdentityTestData.DefaultPin;
        values["static_otp"] = IdentityTestData.DefaultEmailOtp;
        values["test_mode_enabled"] = "false";
        values["core_fund"] = s.FundId.ToString();
        values["core_proposal"] = proposalId.ToString();
        values["core_election"] = electionId.ToString();
        values["core_election_fund"] = electionFund.ToString();
        values["core_member"] = s.Member.Id.ToString();
        values["core_owner"] = s.Owner.Id.ToString();
        values["core_admin"] = s.Admin.Id.ToString();
        values["core_member_wallet"] = s.MemberWallet.ToString();
        values["core_owner_wallet"] = s.OwnerWallet.ToString();

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "backend", "postman", "generate_postman_collection.py")))
            root = root.Parent ?? throw new InvalidOperationException("Repository root unavailable.");

        var output = Path.Combine(Path.GetTempPath(), "spendwise-g4");
        Directory.CreateDirectory(output);
        var environment = Path.Combine(output, "newman-private.json");
        await File.WriteAllTextAsync(environment, JsonSerializer.Serialize(new
        {
            name = "G4 SQL02 E2E Fixture",
            values = values.Select(v => new { key = v.Key, value = v.Value, enabled = true })
        }));

        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = root.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node_modules", "npm", "bin", "npx-cli.js"),
            "--no-install",
            "newman",
            "run",
            "backend/postman/SpendWise-V1-Full-E2E.postman_collection.json",
            "-e",
            environment,
            "--reporters",
            "json,junit",
            "--reporter-json-export",
            Path.Combine(output, "g4-newman.json"),
            "--reporter-junit-export",
            Path.Combine(output, "g4-newman.xml"),
            "--timeout",
            "300000",
            "--timeout-request",
            "15000"
        })
        {
            start.ArgumentList.Add(argument);
        }

        start.ArgumentList.Add("--export-environment");
        start.ArgumentList.Add(environment);

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(6));

        await File.WriteAllTextAsync(Path.Combine(output, "g4-newman-cli.log"), await stdout + await stderr);

        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "g4-newman.json")));
        var run = report.RootElement.GetProperty("run");
        Assert.True(run.GetProperty("stats").GetProperty("requests").GetProperty("total").GetInt32() > 0, "No Newman requests executed.");

        var failures = run.GetProperty("failures").EnumerateArray().Select(x => x.GetProperty("source").GetProperty("name").GetString() + ": " + x.GetProperty("error").GetProperty("message").GetString()).ToArray();
        Assert.True(process.ExitCode == 0 && failures.Length == 0, string.Join("; ", failures));

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<V1BroadcastService>().ProcessDueAsync(default);

        await using var verify = context();
        Assert.Equal("Settled", (await verify.V1ActivityProposals.SingleAsync(x => x.Id == proposalId)).State);
        Assert.Equal(V1ElectionPhase.Failed, (await verify.V1Elections.SingleAsync(x => x.Id == electionId)).Phase);
        Assert.Equal("NoVotes", (await verify.V1Elections.SingleAsync(x => x.Id == electionId)).FailureReason);
        Assert.Equal(s.Owner.Id, (await verify.V1Funds.SingleAsync(x => x.Id == electionFund)).OwnerUserId);
        Assert.All(await verify.V1MaintenancePlans.ToListAsync(), p => Assert.NotNull(p.ClosedAt ?? p.CancelledAt));
        Assert.True(await verify.V1Broadcasts.AnyAsync(b => b.State == "Published"));
        foreach (var account in await verify.LedgerAccounts.ToListAsync())
            Assert.Equal(account.Balance, await verify.LedgerEntries.Where(e => e.LedgerAccountId == account.Id).SumAsync(e => e.Direction == LedgerEntryDirection.Debit ? e.Amount : -e.Amount));
        Assert.Equal(0, await CoreAcceptanceData.HeldAsync(verify, s.FundId));
        Assert.True(await verify.FinancialTransactions.AnyAsync(t => t.Type == FinancialTransactionType.Refund && t.Status == FinancialTransactionStatus.Completed));

        client.Dispose();
        factory.Dispose();
    }
}
