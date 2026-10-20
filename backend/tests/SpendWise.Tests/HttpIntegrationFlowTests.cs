using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Banking;
using SpendWise.Application.Finance;
using SpendWise.Application.Identity;
using SpendWise.Domain.Banking;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Các test đi qua routing, model binding, middleware, Controller và MediatR thật.
public sealed class HttpIntegrationFlowTests
{
    [Fact]
    public async Task Authentication_supports_rotation_logout_and_immediate_revocation()
    {
        // Arrange: đăng ký tài khoản và xác nhận endpoint bảo vệ nhận access token.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var registered = await RegisterAsync(client, "HTTP auth user");
        UseToken(client, registered.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        // Act: login tạo phiên mới và refresh token được rotation.
        var login = await LoginAsync(client, registered.User.Email, "StrongPass1");
        var refreshedResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(login.RefreshToken, "Rotated HTTP client"));
        refreshedResponse.EnsureSuccessStatusCode();
        var refreshed = (await refreshedResponse.Content.ReadFromJsonAsync<AuthResponse>())!;

        // Assert: refresh token cũ không được dùng lại.
        var replay = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // Act: logout đúng phiên bằng credential vừa rotation.
        UseToken(client, refreshed.AccessToken);
        var logout = await client.PostAsJsonAsync(
            "/api/auth/logout",
            new RefreshRequest(refreshed.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // Assert: cả access token và refresh token của phiên đã bị thu hồi ngay.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/users/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        var invalidRefresh = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(refreshed.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, invalidRefresh.StatusCode);
    }

    [Fact]
    public async Task Validation_and_error_contract_return_400_404_409_in_requested_language()
    {
        // Arrange: tạo người dùng đã xác thực.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var auth = await RegisterAsync(client, "HTTP validation user");
        UseToken(client, auth.AccessToken);

        // Act và Assert: enum ví ngoài tập cho phép bị từ chối bằng ProblemDetails 400.
        var invalidEnum = await client.PostAsJsonAsync(
            "/api/wallets",
            new CreateWalletRequest("Crypto", "Cryptocurrency"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidEnum.StatusCode);
        Assert.Equal("invalid_wallet_type", await ProblemCodeAsync(invalidEnum));

        // Act và Assert: input email sai bị chặn ở biên HTTP/Application.
        client.DefaultRequestHeaders.Authorization = null;
        var invalidRequest = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                "Invalid",
                "not-an-email",
                IdentityTestData.NewPhone(),
                "StrongPass1",
                "StrongPass1"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidRequest.StatusCode);

        // Act: yêu cầu tài nguyên không tồn tại bằng locale tiếng Việt.
        UseToken(client, auth.AccessToken);
        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("vi-VN");
        var missingVi = await client.GetAsync($"/api/wallets/{Guid.NewGuid()}");

        // Assert: 404 giữ code ổn định nhưng title được bản địa hóa.
        Assert.Equal(HttpStatusCode.NotFound, missingVi.StatusCode);
        var viProblem = await ProblemAsync(missingVi);
        Assert.Equal("wallet_not_found", viProblem.GetProperty("code").GetString());
        Assert.Equal("Không tìm thấy ví.", viProblem.GetProperty("title").GetString());

        // Act: cùng lỗi bằng locale tiếng Anh.
        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US");
        var missingEn = await client.GetAsync($"/api/wallets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missingEn.StatusCode);
        Assert.Equal(
            "Wallet not found.",
            (await ProblemAsync(missingEn)).GetProperty("title").GetString());

        // Act và Assert: tạo trùng cấu hình danh mục trả Conflict 409.
        var category = new CreateCategoryRequest("HTTP duplicate", "Expense");
        (await client.PostAsJsonAsync("/api/categories", category)).EnsureSuccessStatusCode();
        var duplicate = await client.PostAsJsonAsync("/api/categories", category);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("category_exists", await ProblemCodeAsync(duplicate));
    }

    [Fact]
    public async Task Financial_http_flow_keeps_bank_direct_p2p_outside_wallet_ledger()
    {
        // Arrange: tạo Alice, Bob và lấy ví mặc định do registration tạo nguyên tử.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var alice = await RegisterAsync(client, "HTTP Alice");
        var bob = await RegisterAsync(client, "HTTP Bob");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            db.BankConnections.Add(new BankConnection
            {
                UserId = bob.User.Id,
                Provider = BankProvider.SePayBankHub,
                ExternalAccountId = $"http-bob-{bob.User.Id:N}",
                BankBrandName = "Test Bank",
                AccountHolderName = bob.User.FullName,
                MaskedAccountNumber = "******1234",
                Status = BankConnectionStatus.Active,
                LinkedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        UseToken(client, alice.AccessToken);
        var aliceMain = Assert.Single((await client.GetFromJsonAsync<IReadOnlyList<WalletDto>>(
            "/api/wallets"))!);
        UseToken(client, bob.AccessToken);
        var bobMain = Assert.Single((await client.GetFromJsonAsync<IReadOnlyList<WalletDto>>(
            "/api/wallets"))!);

        // Arrange: tạo danh mục và nạp 10.000 đồng cho Alice qua endpoint Income.
        UseToken(client, alice.AccessToken);
        var incomeCategory = await CreateCategoryAsync(client, "HTTP salary", "Income");
        var expenseCategory = await CreateCategoryAsync(client, "HTTP food", "Expense");
        var incomeEvidence = await IdentityTestData.UploadEvidenceAsync(client, "income.png");
        var expenseEvidence = await IdentityTestData.UploadEvidenceAsync(client, "expense.png");
        var income = await client.PostAsJsonAsync(
            "/api/transactions/income",
            new CreateManualTransactionRequest(
                aliceMain.Id,
                incomeCategory.Id,
                10_000,
                "HTTP income",
                IdempotencyKey: "http-income",
                SecurityPin: IdentityTestData.DefaultPin,
                EvidenceIds: [incomeEvidence.Id]));
        Assert.Equal(HttpStatusCode.Created, income.StatusCode);

        // Act: ghi Expense, chuyển nội bộ và tạo lệnh P2P bank-direct.
        var expense = await client.PostAsJsonAsync(
            "/api/transactions/expense",
            new CreateManualTransactionRequest(
                aliceMain.Id,
                expenseCategory.Id,
                1_000,
                "HTTP expense",
                IdempotencyKey: "http-expense",
                SecurityPin: IdentityTestData.DefaultPin,
                EvidenceIds: [expenseEvidence.Id]));
        Assert.Equal(HttpStatusCode.Created, expense.StatusCode);
        var savingsResponse = await client.PostAsJsonAsync(
            "/api/wallets",
            new CreateWalletRequest("HTTP Savings", "Savings"));
        savingsResponse.EnsureSuccessStatusCode();
        var savings = (await savingsResponse.Content.ReadFromJsonAsync<WalletDto>())!;
        var internalTransfer = await client.PostAsJsonAsync(
            "/api/transfers/internal",
            new InternalTransferRequest(
                aliceMain.Id,
                savings.Id,
                2_000,
                "HTTP internal",
                "http-internal",
                IdentityTestData.DefaultPin));
        Assert.Equal(HttpStatusCode.Created, internalTransfer.StatusCode);
        var completedTransfer = (await internalTransfer.Content.ReadFromJsonAsync<TransferDto>())!;
        Assert.Equal("Completed", completedTransfer.Status);
        Assert.NotNull(completedTransfer.LedgerTransactionId);
        if (internalTransfer.Headers.Location is { } location)
        {
            // Any advertised resource must be readable; do not fabricate an internal-transfer GET route.
            using var resource = await client.GetAsync(location);
            Assert.Equal(HttpStatusCode.OK, resource.StatusCode);
        }
        using var retry = await client.PostAsJsonAsync("/api/transfers/internal",
            new InternalTransferRequest(aliceMain.Id, savings.Id, 2_000, "HTTP internal", "http-internal", IdentityTestData.DefaultPin));
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var replayedTransfer = (await retry.Content.ReadFromJsonAsync<TransferDto>())!;
        Assert.True(replayedTransfer.Replayed);
        Assert.Equal(completedTransfer.Id, replayedTransfer.Id);
        Assert.Equal(completedTransfer.LedgerTransactionId, replayedTransfer.LedgerTransactionId);
        if (retry.Headers.Location is { } replayLocation)
        {
            using var resource = await client.GetAsync(replayLocation);
            Assert.Equal(HttpStatusCode.OK, resource.StatusCode);
        }
        using (var transferScope = factory.Services.CreateScope())
        {
            var db = transferScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            Assert.Equal(1, await db.Transfers.CountAsync(x => x.Id == completedTransfer.Id));
            Assert.Equal(1, await db.LedgerTransactions.CountAsync(x => x.BusinessId == completedTransfer.Id && x.BusinessType == "InternalTransfer"));
        }
        var p2p = await client.PostAsJsonAsync(
            "/api/transfers",
            new CreateBankDirectTransferRequest(
                bob.User.Id,
                2_000,
                "HTTP P2P",
                "http-p2p"));
        Assert.Equal(HttpStatusCode.Created, p2p.StatusCode);
        var bankPayment = (await p2p.Content.ReadFromJsonAsync<BankDirectPaymentDto>())!;
        Assert.Equal("Pending", bankPayment.Status);

        // MoneyRequest cũ đã bị retire; không còn gửi request tới route cũ.

        // Assert: chỉ Income/Expense/Internal thay đổi ví; bank-direct P2P không chạm số dư hai bên.
        UseToken(client, alice.AccessToken);
        var aliceWallets = (await client.GetFromJsonAsync<IReadOnlyList<WalletDto>>(
            "/api/wallets"))!;
        Assert.Equal(7_000, aliceWallets.Single(wallet => wallet.Id == aliceMain.Id).AvailableBalance);
        Assert.Equal(2_000, aliceWallets.Single(wallet => wallet.Id == savings.Id).AvailableBalance);
        UseToken(client, bob.AccessToken);
        var bobWallets = (await client.GetFromJsonAsync<IReadOnlyList<WalletDto>>(
            "/api/wallets"))!;
        Assert.Equal(0, bobWallets.Single(wallet => wallet.Id == bobMain.Id).AvailableBalance);

        // Assert: P2P bank-direct không tạo fake TransferOut/Income/Expense.
        UseToken(client, alice.AccessToken);
        var history = await client.GetFromJsonAsync<PagedResult<TransactionDto>>(
            "/api/transactions?pageSize=50");
        Assert.Contains(history!.Items, item => item.Type == "Income");
        Assert.Contains(history.Items, item => item.Type == "Expense");
        Assert.Contains(history.Items, item => item.Type == "InternalTransfer");
        Assert.DoesNotContain(history.Items, item => item.Type is "TransferOut" or "TransferIn");
    }

    [Fact]
    public async Task Auth_rate_limit_returns_429_after_fixed_window_is_exhausted()
    {
        // Arrange: tất cả request dùng cùng IP của test client và cùng auth partition.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        // Act: mười lần đầu đi qua handler, lần thứ mười một bị middleware chặn.
        var responses = new List<HttpResponseMessage>();
        for (var attempt = 0; attempt < 11; attempt++)
        {
            responses.Add(await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("missing@test.local", "StrongPass1")));
        }

        // Assert: policy cố định trả 429 đúng khi quota đã hết.
        Assert.All(responses.Take(10), response =>
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, responses[10].StatusCode);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Financial_rate_limit_returns_429_after_fixed_window_is_exhausted()
    {
        // Arrange: Cấu hình FinancialPermitLimit = 5 để kiểm tra chính xác policy "financial"
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:FinancialPermitLimit"] = "5"
        });
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAsync(client, "Financial Rate Limit User");
        UseToken(client, user.AccessToken);

        // Act: Gửi 6 request liên tiếp vào endpoint có [EnableRateLimiting("financial")]
        var responses = new List<HttpResponseMessage>();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            using var form = new MultipartFormDataContent();
            using var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", $"receipt_{attempt}.png");
            responses.Add(await client.PostAsync("/api/evidence", form));
        }

        // Assert: 5 request đầu được Controller xử lý thành công (201 Created), request thứ 6 nhận 429 Too Many Requests
        Assert.All(responses.Take(5), response =>
            Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, responses[5].StatusCode);
        foreach (var response in responses)
        {
            response.Dispose();
        }
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

    private static async Task<CategoryDto> CreateCategoryAsync(
        HttpClient client,
        string name,
        string kind)
    {
        var response = await client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest(name, kind));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    private static void UseToken(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        return (await ProblemAsync(response)).GetProperty("code").GetString();
    }
}
