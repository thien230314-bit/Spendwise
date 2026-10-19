using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Admin;
using SpendWise.Application.Finance;
using SpendWise.Application.Groups;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Các test này đi qua middleware JWT/authorization và Controller thật để chống IDOR.
public sealed class HttpAuthorizationTests
{
    [Fact]
    public async Task SePay_test_mode_accounts_requires_admin_and_is_hidden_outside_development()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        const string route = "/api/admin/integrations/sepay/test-mode/bank-accounts";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);

        var user = await RegisterAsync(client, "Test Mode user");
        UseToken(client, user.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);

        await PromoteToAdminAsync(factory, user.User.Id);
        var admin = await LoginAsync(client, user.User.Email, "StrongPass1");
        UseToken(client, admin.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedRequest_ToPrivateWalletRoute_Returns401()
    {
        // Arrange: client không có access token.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        // Act: gọi endpoint ví cá nhân.
        var response = await client.GetAsync("/api/wallets");

        // Assert: middleware chặn trước Controller.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("expired")]
    [InlineData("revoked")]
    public async Task M03_Jwt_challenge_preserves_problem_contract_and_advertises_bearer(string scenario)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US");
        if (scenario == "malformed")
            UseToken(client, "not-a-jwt");
        else if (scenario is "expired" or "revoked")
        {
            var auth = await RegisterAsync(client, "Challenge owner");
            UseToken(client, auth.AccessToken);
            if (scenario == "expired")
            {
                var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var validToken = tokenHandler.ReadJwtToken(auth.AccessToken);
                var configuration = factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
                var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)),
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
                var expiredToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(validToken.Issuer,
                    validToken.Audiences.Single(), validToken.Claims.Where(claim => claim.Type is not "exp" and not "nbf"),
                    DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-2), credentials);
                UseToken(client, tokenHandler.WriteToken(expiredToken));
            }
            if (scenario == "revoked")
                Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(auth.RefreshToken))).StatusCode);
        }

        using var response = await client.GetAsync("/api/wallets");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(401, problem.GetProperty("status").GetInt32());
        Assert.Equal("unauthorized", problem.GetProperty("code").GetString());
        Assert.Equal("Authentication is required.", problem.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task UserCannotReadAnotherUsersWallet()
    {
        // Arrange: hai tài khoản được đăng ký qua API và có ví mặc định riêng.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var first = await RegisterAsync(client, "Wallet owner");
        var second = await RegisterAsync(client, "Wrong user");
        UseToken(client, first.AccessToken);
        var wallets = await client.GetFromJsonAsync<IReadOnlyList<WalletDto>>("/api/wallets");
        var privateWalletId = Assert.Single(wallets!).Id;

        // Act: người thứ hai gửi ID ví của người thứ nhất.
        UseToken(client, second.AccessToken);
        var response = await client.GetAsync($"/api/wallets/{privateWalletId}");

        // Assert: policy che giấu tài nguyên người khác bằng 404.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NormalUserCannotAccessAdminRoute()
    {
        // Arrange: access token chỉ có role User.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAsync(client, "Normal user");
        UseToken(client, user.AccessToken);

        // Act: truy cập route yêu cầu policy SystemAdmin.
        var response = await client.GetAsync("/api/admin/dashboard");

        // Assert: đã xác thực nhưng không đúng role nên trả 403.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminStillCannotUsePrivateWalletEndpointForAnotherUser()
    {
        // Arrange: tạo chủ ví và một user sẽ được nâng role trong database test.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await RegisterAsync(client, "Private owner");
        UseToken(client, owner.AccessToken);
        var wallets = await client.GetFromJsonAsync<IReadOnlyList<WalletDto>>("/api/wallets");
        var privateWalletId = Assert.Single(wallets!).Id;
        var adminCandidate = await RegisterAsync(client, "Admin candidate");
        await PromoteToAdminAsync(factory, adminCandidate.User.Id);
        var admin = await LoginAsync(client, adminCandidate.User.Email, "StrongPass1");

        // Act: role SystemAdmin gọi user-private route với ID của người khác.
        UseToken(client, admin.AccessToken);
        var response = await client.GetAsync($"/api/wallets/{privateWalletId}");

        // Assert: SystemAdmin bị chặn ngay tại ranh giới User API.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NonMemberCannotReadGroupAndOwnerCannotReadMembersWallet()
    {
        // Arrange: owner tạo group; member và người ngoài có ví riêng.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await RegisterAsync(client, "Group owner");
        var member = await RegisterAsync(client, "Group member");
        var outsider = await RegisterAsync(client, "Group outsider");
        UseToken(client, owner.AccessToken);
        var groupResponse = await client.PostAsJsonAsync(
            "/api/groups",
            new CreateGroupRequest("Private group", null, "Friends", SecurityPin: IdentityTestData.DefaultPin));
        groupResponse.EnsureSuccessStatusCode();
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();

        // Tạo membership thật trong database; không thêm bất kỳ quyền ví nào cho Owner.
        await AddMemberAsync(factory, group!.Id, member.User.Id);
        UseToken(client, member.AccessToken);
        var memberWallets = await client.GetFromJsonAsync<IReadOnlyList<WalletDto>>("/api/wallets");
        var memberWalletId = Assert.Single(memberWallets!).Id;

        // Act: người ngoài xem group; sau đó Owner thử xem ví cá nhân của Member.
        UseToken(client, outsider.AccessToken);
        var groupResult = await client.GetAsync($"/api/groups/{group.Id}");
        UseToken(client, owner.AccessToken);
        var walletResult = await client.GetAsync($"/api/wallets/{memberWalletId}");

        // Assert: group privacy trả 403, còn private finance che bằng 404.
        Assert.Equal(HttpStatusCode.Forbidden, groupResult.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, walletResult.StatusCode);
    }

    [Fact]
    public async Task SupportCase_CannotLinkAnotherUsersTransaction()
    {
        // Arrange: user thứ nhất tạo một giao dịch thu hoàn chỉnh qua HTTP.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await RegisterAsync(client, "Transaction owner");
        var attacker = await RegisterAsync(client, "Support attacker");
        UseToken(client, owner.AccessToken);
        var wallets = await client.GetFromJsonAsync<IReadOnlyList<WalletDto>>("/api/wallets");
        var categoryResponse = await client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest("Salary", "Income"));
        categoryResponse.EnsureSuccessStatusCode();
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
        var evidence = await IdentityTestData.UploadEvidenceAsync(client);
        var incomeResponse = await client.PostAsJsonAsync(
            "/api/transactions/income",
            new CreateManualTransactionRequest(
                Assert.Single(wallets!).Id,
                category!.Id,
                100_000m,
                "Private salary",
                SecurityPin: IdentityTestData.DefaultPin,
                EvidenceIds: [evidence.Id]));
        incomeResponse.EnsureSuccessStatusCode();
        var income = await incomeResponse.Content.ReadFromJsonAsync<TransactionDto>();

        // Act: user thứ hai thử gắn transaction ID không thuộc mình vào support case.
        UseToken(client, attacker.AccessToken);
        var response = await client.PostAsJsonAsync(
            "/api/cases/support",
            new CreateSupportCaseRequest("transaction", "Normal", income!.Id));

        // Assert: handler che tài nguyên chéo bằng 404 và không tạo support case.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Empty(db.SupportCases);
    }

    [Fact]
    public async Task Evidence_download_is_authenticated_and_owner_only()
    {
        // Arrange: owner upload evidence qua multipart API và người khác có phiên riêng.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await RegisterAsync(client, "Evidence owner");
        var stranger = await RegisterAsync(client, "Evidence stranger");
        UseToken(client, owner.AccessToken);
        var evidence = await IdentityTestData.UploadEvidenceAsync(client, "private.png");

        // Assert: owner tải được bytes, người khác nhận 404 để không lộ tài nguyên.
        var owned = await client.GetAsync($"/api/evidence/{evidence.Id}");
        Assert.Equal(HttpStatusCode.OK, owned.StatusCode);
        Assert.Equal("image/png", owned.Content.Headers.ContentType!.MediaType);
        UseToken(client, stranger.AccessToken);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/evidence/{evidence.Id}")).StatusCode);

        // Assert: request không có access token bị middleware chặn.
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync($"/api/evidence/{evidence.Id}")).StatusCode);
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string name)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return await IdentityTestData.RegisterAndSetupPinAsync(client, name);
    }

    private static async Task<AuthResponse> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return await IdentityTestData.LoginAndVerifyPinAsync(client, email, password);
    }

    private static void UseToken(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static async Task PromoteToAdminAsync(SpendWiseApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = await db.Users.FindAsync(userId);
        user!.SystemRole = SystemRole.SystemAdmin;
        await db.SaveChangesAsync();
    }

    private static async Task AddMemberAsync(
        SpendWiseApiFactory factory,
        Guid groupId,
        Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        db.GroupMembers.Add(new SpendWise.Domain.Groups.GroupMember
        {
            GroupId = groupId,
            UserId = userId,
            Role = SpendWise.Domain.Groups.GroupRole.Member,
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
