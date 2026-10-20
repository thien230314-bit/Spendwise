using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.Operations;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Identity;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class CoreNewmanFactAttribute : FactAttribute
{
    public CoreNewmanFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SPENDWISE_RUN_CORE_NEWMAN") != "1")
            Skip = "Explicit SQL02/Core Newman opt-in required; providers disabled.";
    }
}

[Trait("Category", "SqlServer")]
[Trait("Category", "CoreNewman")]
public sealed class CoreNewmanSqlTests
{
    [CoreNewmanFact]
    public async Task Canonical_Core_Newman_runs_on_owned_SQL02_and_reconciles_final_business_state()
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(kestrel: true);
        CoreFundSeed s; Guid proposalId, electionId, electionFund;
        var values = new Dictionary<string, string>();
        await using (var db = f.Context())
        {
            s = await CoreAcceptanceData.SeedAsync(db, ledgerOpening: true);
            s.Admin.PasswordHash = f.Factory.Services.GetRequiredService<IPasswordService>().Hash("StrongPass1");
            db.Users.Update(s.Admin);
            db.Categories.AddRange(new Category { Key = "synthetic-income", Name = "Synthetic income", Kind = CategoryKind.Income, IsSystem = true }, new Category { Key = "synthetic-expense", Name = "Synthetic expense", Kind = CategoryKind.Expense, IsSystem = true });
            await db.SaveChangesAsync();
            var members = await db.V1FundMemberships.Where(m => m.FundId == s.FundId).ToListAsync();
            var fund = await db.V1Funds.SingleAsync(); await db.V1Funds.Where(x => x.Id == fund.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.ActivityClosedAt, (DateTime?)f.Clock.UtcNow.AddDays(-2)));
            var p = new V1ActivityProposal { FundId = s.FundId, ProposedByUserId = s.Owner.Id, OwnershipRevision = fund.OwnershipRevision, TargetVnd = 1_000_000, State = "ApprovedAwaitingSettlement", IdempotencyKey = "synthetic-newman-period", OpenedAt = f.Clock.UtcNow.AddDays(-1) };
            db.V1ActivityProposals.Add(p); db.V1ActivityVoters.AddRange(members.Select(m => new V1ActivityVoter { ProposalId = p.Id, UserId = m.UserId, MembershipId = m.Id, Support = true })); proposalId = p.Id;
            var ef = await CoreAcceptanceData.Funds(db, f.Clock).CreateAsync(s.Owner.Id, new("Synthetic election", "Free", null, IdentityTestData.DefaultPin), default); electionFund = ef.Id;
            var em = new V1FundMembership { FundId = ef.Id, UserId = s.Member.Id, Seat = 2 }; db.V1FundMemberships.Add(em);
            var e = new V1Election { FundId = ef.Id, IncumbentUserId = s.Owner.Id, OpenedByUserId = s.Member.Id, Phase = V1ElectionPhase.Nomination, PhaseStartedAt = f.Clock.UtcNow.AddDays(-3), PolicyJson = JsonSerializer.Serialize(new V1ElectionPolicy().Snapshot()), InactivityEvidenceId = "synthetic-owned-fixture" };
            db.V1Elections.Add(e); db.V1ElectionVoters.Add(new() { ElectionId = e.Id, UserId = s.Member.Id, MembershipId = em.Id }); db.V1ElectionCandidates.Add(new() { ElectionId = e.Id, UserId = s.Member.Id }); electionId = e.Id;
            await db.SaveChangesAsync();
        }
        foreach (var (key, actor) in new[] { ("core_owner_token", s.Owner), ("core_member_token", s.Member), ("core_admin_token", s.Admin) })
        { await f.ActorAsync(actor); values[key] = f.Client.DefaultRequestHeaders.Authorization!.Parameter!; }
        values["core_maintenance_end"] = f.Clock.UtcNow.AddHours(2).ToString("O");
        values["baseUrl"] = f.Factory.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.Single().TrimEnd('/');
        using (var ready = await f.Client.GetAsync(values["baseUrl"] + "/health")) Assert.True(ready.IsSuccessStatusCode, "Loopback readiness failed before Newman.");
        values["admin_email"] = s.Admin.Email; values["admin_password"] = values["user_password"] = "StrongPass1";
        values["default_pin"] = IdentityTestData.DefaultPin; values["static_otp"] = IdentityTestData.DefaultEmailOtp; values["test_mode_enabled"] = "false";
        values["core_fund"] = s.FundId.ToString(); values["core_proposal"] = proposalId.ToString(); values["core_election"] = electionId.ToString(); values["core_election_fund"] = electionFund.ToString();
        values["core_member"] = s.Member.Id.ToString(); values["core_owner"] = s.Owner.Id.ToString(); values["core_admin"] = s.Admin.Id.ToString(); values["core_member_wallet"] = s.MemberWallet.ToString(); values["core_owner_wallet"] = s.OwnerWallet.ToString();
        if (Environment.GetEnvironmentVariable("SPENDWISE_NEWMAN_FOLDER") is not null)
        {
            values["userA_accessToken"] = values["core_owner_token"]; values["userB_accessToken"] = values["core_member_token"];
        }
        var root = new DirectoryInfo(AppContext.BaseDirectory); while (!File.Exists(Path.Combine(root.FullName, "backend", "postman", "generate_postman_collection.py"))) root = root.Parent ?? throw new InvalidOperationException("Repository root unavailable.");
        var output = Path.Combine(Path.GetTempPath(), "spendwise-task17"); Directory.CreateDirectory(output);
        var environment = Path.Combine(output, "newman-private.json");
        await File.WriteAllTextAsync(environment, JsonSerializer.Serialize(new { name = "Owned SQL02 Core fixture", values = values.Select(v => new { key = v.Key, value = v.Value, enabled = true }) }));
        var start = new ProcessStartInfo("node") { WorkingDirectory = root.FullName, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node_modules", "npm", "bin", "npx-cli.js"), "--no-install", "newman", "run", "backend/postman/SpendWise-V1-Full-E2E.postman_collection.json", "-e", environment, "--reporters", "json,junit", "--reporter-json-export", Path.Combine(output, "core-newman.json"), "--reporter-junit-export", Path.Combine(output, "core-newman.xml"), "--timeout", "300000", "--timeout-request", "15000" }) start.ArgumentList.Add(argument);
        if (Environment.GetEnvironmentVariable("SPENDWISE_NEWMAN_FOLDER") is { Length: > 0 } selectedFolder)
        { start.ArgumentList.Add("--folder"); start.ArgumentList.Add(selectedFolder); }
        start.ArgumentList.Add("--export-environment"); start.ArgumentList.Add(environment);
        using var process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(6));
        await File.WriteAllTextAsync(Path.Combine(output, "newman-cli.log"), await stdout + await stderr);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "core-newman.json")));
        var run = report.RootElement.GetProperty("run");
        Assert.True(run.GetProperty("stats").GetProperty("requests").GetProperty("total").GetInt32() > 0, "No Newman requests executed.");
        var failures = run.GetProperty("failures").EnumerateArray().Select(x => x.GetProperty("source").GetProperty("name").GetString() + ": " + x.GetProperty("error").GetProperty("message").GetString()).ToArray();
        Assert.True(process.ExitCode == 0 && failures.Length == 0, string.Join("; ", failures));
        using (var scope = f.Factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<V1BroadcastService>().ProcessDueAsync(default);
        await using var verify = f.Context();
        Assert.Equal("Settled", (await verify.V1ActivityProposals.SingleAsync()).State);
        Assert.Equal(V1ElectionPhase.Failed, (await verify.V1Elections.SingleAsync()).Phase);
        Assert.Equal("NoVotes", (await verify.V1Elections.SingleAsync()).FailureReason);
        Assert.Equal(s.Owner.Id, (await verify.V1Funds.SingleAsync(x => x.Id == electionFund)).OwnerUserId);
        Assert.All(await verify.V1MaintenancePlans.ToListAsync(), p => Assert.NotNull(p.ClosedAt ?? p.CancelledAt));
        Assert.True(await verify.V1Broadcasts.AnyAsync(b => b.State == "Published"));
        foreach (var account in await verify.LedgerAccounts.ToListAsync())
            Assert.Equal(account.Balance, await verify.LedgerEntries.Where(e => e.LedgerAccountId == account.Id).SumAsync(e => e.Direction == LedgerEntryDirection.Debit ? e.Amount : -e.Amount));
        Assert.Equal(0, await CoreAcceptanceData.HeldAsync(verify, s.FundId));
        Assert.True(await verify.FinancialTransactions.AnyAsync(t => t.Type == FinancialTransactionType.Refund && t.Status == FinancialTransactionStatus.Completed));
    }
}
