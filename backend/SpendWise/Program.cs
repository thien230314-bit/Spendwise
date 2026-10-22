using System.Globalization;
using Microsoft.AspNetCore.Localization;
using SpendWise.API.Infrastructure;
using SpendWise.Application;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SpendWise.API.Realtime;
using SpendWise.Application.Notifications;
using SpendWise.Infrastructure.Persistence;
using System.Threading.RateLimiting;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
var isAdminE2E = builder.Environment.IsEnvironment(AdminE2EStartup.EnvironmentName);
AdminE2EStartup.LoadConfiguration(builder, args);

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    // Giữ nguyên ValidationProblemDetails nhưng bảo đảm lỗi model binding theo ngôn ngữ request.
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(
                item => item.Key,
                item => item.Value!.Errors
                    .Select(error => ApiErrorLocalizer.LocalizeValidationMessage(error.ErrorMessage))
                    .ToArray());
        var details = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = ApiErrorLocalizer.Localize("invalid_input", "One or more validation errors occurred."),
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
        };
        details.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(details);
    };
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddHealthChecks();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddResponseCompression(options =>
{
    // Nén response JSON qua HTTPS để giảm dữ liệu truyền cho frontend.
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);

// Chỉ tin các proxy/reverse proxy đã khai báo rõ trong cấu hình triển khai.
var forwardedHeadersSection = builder.Configuration.GetSection("ForwardedHeaders");
var knownProxyValues = forwardedHeadersSection.GetSection("KnownProxies").Get<string[]>() ?? [];
var knownNetworkValues = forwardedHeadersSection.GetSection("KnownNetworks").Get<string[]>() ?? [];
var knownProxies = knownProxyValues.Select(ParseIpAddress).ToArray();
var knownNetworks = knownNetworkValues.Select(ParseIpNetwork).ToArray();
var useForwardedHeaders = knownProxies.Length > 0 || knownNetworks.Length > 0;
if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
            | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;

        foreach (var proxy in knownProxies)
        {
            options.KnownProxies.Add(proxy);
        }

        foreach (var network in knownNetworks)
        {
            options.KnownIPNetworks.Add(network);
        }
    });
}

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) && builder.Environment.IsDevelopment())
{
    jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    builder.Configuration["Jwt:Key"] = jwtKey;
}
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Jwt:Key must be supplied through secure configuration outside Development.");
}

// HS256 cần khóa bí mật có ít nhất 256 bit để tránh khóa yếu trong production.
var jwtKeyBytes = JwtSigningKey.Validate(jwtKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // SignalR trên trình duyệt gửi access token qua query khi mở WebSocket/SSE.
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken)
                && path.StartsWithSegments("/hubs"))
            {
                // Chỉ đọc query token cho Hub, không mở cơ chế này cho API thông thường.
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            // JWT chỉ hợp lệ khi UserId và SessionId đều là Guid đúng định dạng.
            var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var sessionIdValue = context.Principal?.FindFirstValue("sid");
            if (!Guid.TryParse(userIdValue, out var userId)
                || !Guid.TryParse(sessionIdValue, out var sessionId))
            {
                context.Fail("Access token identity claims are invalid.");
                return;
            }

            // Kiểm tra lại trạng thái tài khoản và phiên để token đã thu hồi mất hiệu lực ngay.
            var validator = context.HttpContext.RequestServices
                .GetRequiredService<IAccessSessionValidator>();
            if (!await validator.IsValidAsync(
                    userId,
                    sessionId,
                    context.HttpContext.RequestAborted))
            {
                context.Fail("Access session is no longer valid.");
            }
        },
        OnChallenge = async context =>
        {
            // Trả thông báo 401 theo locale thay vì response trống của JWT middleware.
            context.HandleResponse();
            context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
            await WriteLocalizedProblemAsync(
                context.HttpContext,
                StatusCodes.Status401Unauthorized,
                "unauthorized",
                "Authentication is required.");
        },
        OnForbidden = context => WriteLocalizedProblemAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            "forbidden",
            "You do not have permission to perform this action.")
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "SystemAdmin",
        policy => policy.RequireRole("SystemAdmin"));
    options.AddPolicy(
        "User",
        policy => policy.RequireRole("User"));
});

builder.Services.AddV1Authorization(builder.Configuration);

var configuredOrigins = isAdminE2E
    ? [AdminE2EStartup.Origin]
    : builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (configuredOrigins.Length > 0)
    {
        policy
            .WithOrigins(configuredOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }
}));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Admission chỉ cho User chat; named "ai" 15/phút vẫn chạy độc lập bên dưới.
    // Global + endpoint leases được middleware giữ đến khi request thực sự kết thúc.
    // https://learn.microsoft.com/aspnet/core/performance/rate-limit?view=aspnetcore-10.0
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var action = context.GetEndpoint()?.Metadata
            .GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();
        if (action?.ControllerTypeInfo.AsType() != typeof(SpendWise.API.Controllers.AiController)
            || action.MethodInfo.Name != nameof(SpendWise.API.Controllers.AiController.Chat)
            || context.User.Identity?.IsAuthenticated != true
            || !context.User.IsInRole("User")
            || !Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return RateLimitPartition.GetNoLimiter("not-user-chat");
        }

        return RateLimitPartition.GetConcurrencyLimiter($"user-chat:{userId:N}",
            _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = 2,
                QueueLimit = 0
            });
    });
    var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
    var emailPermitLimit = builder.Configuration.GetValue("RateLimiting:EmailVerificationPermitLimit", 10);
    var financialPermitLimit = builder.Configuration.GetValue("RateLimiting:FinancialPermitLimit", 30);
    var aiPermitLimit = builder.Configuration.GetValue("RateLimiting:AiPermitLimit", 15);
    var userDiscoveryPermitLimit = builder.Configuration.GetValue("RateLimiting:UserDiscoveryPermitLimit", 30);
    options.AddPolicy(
        "auth",
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy(
        "emailVerification",
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = emailPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy(
        "financial",
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = financialPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy(
        "ai",
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = aiPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy(
        "userDiscovery",
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = userDiscoveryPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

AdminE2EStartup.ConfigureRuntime(builder);
var app = builder.Build();

if (isAdminE2E)
{
    await AdminE2EStartup.VerifyDatabaseAsync(app.Services);
}

if (app.Environment.IsDevelopment())
{
    await DevelopmentSeeder.SeedAsync(app.Services);
}

// Sắp xếp middleware để lỗi, HTTPS, CORS, xác thực và giới hạn tần suất chạy đúng thứ tự.
if (useForwardedHeaders)
{
    // Khôi phục IP và giao thức gốc sau reverse proxy đã được tin cậy.
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment() && !isAdminE2E)
{
    // Yêu cầu trình duyệt tiếp tục dùng HTTPS ở mọi môi trường ngoài Development.
    app.UseHsts();
}

var supportedCultures = new[] { new CultureInfo("vi-VN"), new CultureInfo("en-US") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("vi-VN"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    ApplyCurrentCultureToResponseHeaders = true
});
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    // Testing công khai tài liệu trong test host để kiểm tra route mà không cần SQL Server thật.
    app.MapOpenApi();
}

// HTTP is allowed only by the explicitly loopback-bound AdminE2E profile.
if (!isAdminE2E)
    app.UseHttpsRedirection();

// Nén response trước khi CORS thêm header và trước khi endpoint tạo nội dung.
app.UseResponseCompression();
app.UseCors("Frontend");
app.UseAuthentication();
// Partition theo identity chỉ sau khi JWT và session đã được xác thực.
// https://learn.microsoft.com/aspnet/core/performance/rate-limit?view=aspnetcore-10.0
app.UseRateLimiter();
app.UseMiddleware<SpendWise.API.Infrastructure.BusinessAccessMiddleware>();
app.UseMiddleware<SpendWise.API.Infrastructure.MustChangePasswordMiddleware>();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
// Liveness chỉ xác nhận tiến trình API còn chạy, không phụ thuộc dịch vụ bên ngoài.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
// Readiness kiểm tra SQL Server; AI được báo riêng và không làm API tài chính mất readiness.
var readinessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
};
app.MapHealthChecks("/health/ready", readinessOptions);
app.MapHealthChecks("/health", readinessOptions);
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapGet("/api/ping", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }))
    .WithName("Ping")
    .WithTags("System");

app.MapFallback(async context =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "index.html"));
});

app.Run();

static IPAddress ParseIpAddress(string value)
{
    if (!IPAddress.TryParse(value, out var address))
    {
        throw new InvalidOperationException(
            $"ForwardedHeaders:KnownProxies contains invalid IP address '{value}'.");
    }

    return address;
}

static System.Net.IPNetwork ParseIpNetwork(string value)
{
    if (!System.Net.IPNetwork.TryParse(value, out var network))
    {
        throw new InvalidOperationException(
            $"ForwardedHeaders:KnownNetworks contains invalid CIDR network '{value}'.");
    }

    return network;
}

static async Task WriteLocalizedProblemAsync(
    HttpContext context,
    int status,
    string code,
    string englishMessage)
{
    context.Response.StatusCode = status;
    var details = new ProblemDetails
    {
        Status = status,
        Title = ApiErrorLocalizer.Localize(code, englishMessage),
        Type = $"https://spendwise.local/errors/{code}"
    };
    details.Extensions["code"] = code;
    details.Extensions["traceId"] = context.TraceIdentifier;

    var service = context.RequestServices.GetRequiredService<IProblemDetailsService>();
    if (!await service.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = details
        }))
    {
        await context.Response.WriteAsJsonAsync(details);
    }
}

public partial class Program;
