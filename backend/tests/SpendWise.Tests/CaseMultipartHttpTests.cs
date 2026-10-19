using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Admin;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class CaseMultipartHttpTests
{
    [Fact]
    public async Task User_adds_support_attachment_through_multipart_using_existing_persistence()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await SeedUserAsync(factory, SystemRole.User);
        var support = await SeedSupportAsync(factory, owner.User.Id);
        UseToken(client, owner.AccessToken);
        using var form = Form("Please investigate", File("proof.png", "image/png", Png()));
        form.Add(new StringContent(Guid.NewGuid().ToString()), "UserId");

        var response = await client.PostAsync($"/api/cases/support/{support.Id}/messages/multipart", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<SupportCaseDetailDto>();
        var history = Assert.Single(detail!.History);
        Assert.Equal(owner.User.Id, history.ActorId);
        var attachment = Assert.Single(history.Attachments);
        Assert.Equal("proof.png", attachment.FileName);
        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>()
            .CaseAttachments.AsNoTracking().SingleAsync();
        Assert.Equal(owner.User.Id, stored.CaseOwnerUserId);
        Assert.Equal(Png(), stored.Content);
        Assert.Equal("image/png", stored.ContentType);
    }

    [Fact]
    public async Task User_adds_five_dispute_attachments_through_multipart()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await SeedUserAsync(factory, SystemRole.User);
        var dispute = await SeedDisputeAsync(factory, owner.User.Id);
        UseToken(client, owner.AccessToken);
        var files = Enumerable.Range(1, CaseAttachmentValidator.MaximumFiles)
            .Select(index => File($"proof-{index}.png", "image/png", Png((byte)index)))
            .ToArray();
        using var form = Form("Additional evidence", files);

        var response = await client.PostAsync($"/api/cases/disputes/{dispute.Id}/messages/multipart", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(
            CaseAttachmentValidator.MaximumFiles,
            await scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().CaseAttachments.CountAsync());
    }

    [Fact]
    public async Task Text_only_multipart_reply_works_when_files_are_optional()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await SeedUserAsync(factory, SystemRole.User);
        var support = await SeedSupportAsync(factory, owner.User.Id);
        UseToken(client, owner.AccessToken);
        using var form = Form("Text only update");

        var response = await client.PostAsync($"/api/cases/support/{support.Id}/messages/multipart", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Empty(db.CaseAttachments);
        Assert.Equal("Text only update", (await db.CaseHistories.SingleAsync()).Message);
    }

    [Fact]
    public async Task Multipart_validation_rejects_too_many_oversized_unsupported_and_empty_files()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await SeedUserAsync(factory, SystemRole.User);
        var support = await SeedSupportAsync(factory, owner.User.Id);
        UseToken(client, owner.AccessToken);

        var invalidFiles = new[]
        {
            Enumerable.Range(0, CaseAttachmentValidator.MaximumFiles + 1)
                .Select(index => File($"proof-{index}.png", "image/png", Png((byte)index))).ToArray(),
            new[] { File("large.png", "image/png", OversizedPng()) },
            new[] { File("malware.exe", "application/octet-stream", [0x4D, 0x5A]) },
            new[] { File("empty.png", "image/png", []) }
        };
        foreach (var files in invalidFiles)
        {
            using var form = Form("Invalid evidence", files);
            var response = await client.PostAsync($"/api/cases/support/{support.Id}/messages/multipart", form);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Empty(db.CaseAttachments);
        Assert.Empty(db.CaseHistories);
    }

    [Fact]
    public async Task Path_like_filename_is_rejected_without_persisting_an_attachment()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await SeedUserAsync(factory, SystemRole.User);
        var dispute = await SeedDisputeAsync(factory, owner.User.Id);
        UseToken(client, owner.AccessToken);
        using var form = Form("Unsafe name", File("../proof.png", "image/png", Png()));

        var response = await client.PostAsync($"/api/cases/disputes/{dispute.Id}/messages/multipart", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().CaseAttachments);
    }

    [Fact]
    public async Task Multipart_routes_require_normal_user_authorization()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await SeedUserAsync(factory, SystemRole.User);
        var admin = await SeedUserAsync(factory, SystemRole.SystemAdmin);
        var support = await SeedSupportAsync(factory, owner.User.Id);

        using (var anonymousForm = Form("Anonymous", File("proof.png", "image/png", Png())))
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsync($"/api/cases/support/{support.Id}/messages/multipart", anonymousForm)).StatusCode);
        UseToken(client, admin.AccessToken);
        using (var adminForm = Form("Admin", File("proof.png", "image/png", Png())))
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.PostAsync($"/api/cases/support/{support.Id}/messages/multipart", adminForm)).StatusCode);
    }

    [Fact]
    public async Task Cross_user_and_business_validation_failures_leave_no_orphan_attachments()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await SeedUserAsync(factory, SystemRole.User);
        var other = await SeedUserAsync(factory, SystemRole.User);
        var dispute = await SeedDisputeAsync(factory, owner.User.Id);
        UseToken(client, other.AccessToken);
        using (var crossUser = Form("Not mine", File("proof.png", "image/png", Png())))
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PostAsync($"/api/cases/disputes/{dispute.Id}/messages/multipart", crossUser)).StatusCode);
        UseToken(client, owner.AccessToken);
        using (var emptyMessage = Form(" ", File("proof.png", "image/png", Png())))
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsync($"/api/cases/disputes/{dispute.Id}/messages/multipart", emptyMessage)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Empty(db.CaseAttachments);
        Assert.Empty(db.CaseHistories);
    }

    private static MultipartFormDataContent Form(string message, params ByteArrayContent[] files)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(message), "Message");
        foreach (var file in files)
        {
            var fileName = file.Headers.ContentDisposition!.FileName!.Trim('"');
            form.Add(file, "Files", fileName);
        }
        return form;
    }

    private static ByteArrayContent File(string fileName, string contentType, byte[] content)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "Files",
            FileName = fileName
        };
        return file;
    }

    private static byte[] Png(byte suffix = 0) => [0x89, 0x50, 0x4E, 0x47, suffix];

    private static byte[] OversizedPng()
    {
        var content = new byte[CaseAttachmentValidator.MaximumFileSize + 1];
        Png().CopyTo(content, 0);
        return content;
    }

    private static async Task<SeededAuth> SeedUserAsync(SpendWiseApiFactory factory, SystemRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var unique = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = "Multipart user",
            Email = $"{unique}@test.local",
            NormalizedEmail = $"{unique}@test.local".ToUpperInvariant(),
            SpendId = unique,
            NormalizedSpendId = unique.ToUpperInvariant(),
            PasswordHash = "test-hash",
            SecurityPinHash = "test-pin-hash",
            EmailVerifiedAt = DateTime.UtcNow,
            Status = UserStatus.Active,
            SystemRole = role
        };
        var session = new UserSession { UserId = user.Id, DeviceName = "Mobile", LastSeenAt = DateTime.UtcNow };
        db.Users.Add(user);
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();
        var accessToken = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, session.Id).Token;
        return new SeededAuth(user, accessToken);
    }

    private static async Task<SupportCase> SeedSupportAsync(SpendWiseApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var item = new SupportCase
        {
            TicketNumber = $"SW-{Guid.NewGuid():N}", LinkedUserId = userId, Category = "other",
            Priority = "normal", Subject = "Mobile support", Description = "Initial request", Status = CaseStatus.Open
        };
        db.SupportCases.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private static async Task<Dispute> SeedDisputeAsync(SpendWiseApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var item = new Dispute
        {
            UserId = userId, LinkedTransactionId = Guid.NewGuid(), Reason = "other", Description = "Initial dispute",
            TransactionReferenceSnapshot = "TX-MULTIPART", TransactionTitleSnapshot = "Test", TransactionAmountSnapshot = 1,
            TransactionOccurredAtSnapshot = DateTime.UtcNow, Status = CaseStatus.Investigating
        };
        db.Disputes.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private static void UseToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private sealed record SeededAuth(User User, string AccessToken);
}
