using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1FundRequestTests
{
    [Theory]
    [InlineData("Free", "Withdrawal")]
    [InlineData("Fixed", "Expense")]
    public async Task Request_permission_replay_review_and_cancellation_never_fake_a_payment(string kind, string requestKind)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var owner = await f.AddUserAsync("Owner"); var member = await f.AddUserAsync("Member");
        var service = V1ElectionTests.Configured(f); var fund = await service.CreateAsync(owner.Id, new("Fund", kind, kind == "Fixed" ? 100 : null, TargetedCorrectnessFixture.SecurityPin), default);
        f.Db.Add(new V1FundMembership { FundId = fund.Id, UserId = member.Id, Seat = 2 }); await f.Db.SaveChangesAsync();
        var request = new V1FundRequestInput(requestKind, 10, member.Id, "Synthetic expense", "request", TargetedCorrectnessFixture.SecurityPin);
        if (kind == "Fixed")
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateFundRequestAsync(member.Id, fund.Id, request, default));
            await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, TargetedCorrectnessFixture.SecurityPin, default);
        }
        var created = await service.CreateFundRequestAsync(member.Id, fund.Id, request, default);
        Assert.Equal("AwaitingOwnerReview", created.State); Assert.Equal(created.Id, (await service.CreateFundRequestAsync(member.Id, fund.Id, request, default)).Id);
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateFundRequestAsync(member.Id, fund.Id, request with { AmountVnd = 11 }, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ReviewFundRequestAsync(member.Id, created.Id, true, "Approve payment", TargetedCorrectnessFixture.SecurityPin, default));
        var approved = await service.ReviewFundRequestAsync(owner.Id, created.Id, true, "Review documented", TargetedCorrectnessFixture.SecurityPin, default);
        Assert.Equal("ApprovedAwaitingFinanceContract", approved.State); Assert.Equal("FINANCE_BLOCKED", approved.ExecutionAvailability);
        Assert.Equal(approved, await service.ReviewFundRequestAsync(owner.Id, created.Id, true, "Review documented", TargetedCorrectnessFixture.SecurityPin, default));
        await service.CancelFundRequestAsync(member.Id, created.Id, TargetedCorrectnessFixture.SecurityPin, default);
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync()); Assert.Empty(await f.Db.V1FundTreasuries.ToListAsync());
        Assert.Single(await f.Db.V1FundRequests.ToListAsync());
        Assert.Equal(3, await f.Db.V1FundActivities.CountAsync(a => a.ReferenceId == created.Id));
    }
}
