using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SpendWise.API.Infrastructure;
using SpendWise.Infrastructure;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminE2EStartupTests
{
    [Theory]
    [InlineData("AdminE2E", 1)]
    [InlineData("Staging", 8)]
    [InlineData("Testing", 8)]
    public void Only_AdminE2E_removes_application_and_DataProtection_workers(string environment, int expected)
    {
        var builder = Builder(environment);
        builder.Services.AddInfrastructure(builder.Configuration);
        // WebApplicationBuilder already registers the framework web-server hosted service.
        Assert.Equal(8, builder.Services.Count(x => x.ServiceType == typeof(IHostedService)));
        Assert.Single(builder.Services, x => x.ServiceType == typeof(IHostedService)
            && x.ImplementationType == typeof(SpendWise.Infrastructure.Services.V1MaintenanceScheduler));
        Configure(builder);
        Assert.Equal(expected, builder.Services.Count(x => x.ServiceType == typeof(IHostedService)));
        Assert.Equal(environment != "AdminE2E", builder.Services.Any(x => x.ServiceType == typeof(IHostedService)
            && x.ImplementationType == typeof(SpendWise.Infrastructure.Services.V1MaintenanceScheduler)));
        Assert.False(builder.Environment.IsDevelopment());
    }

    [Theory]
    [InlineData("ConnectionStrings:DefaultConnection", "Server=DESKTOP-B5KA3LT\\SQLEXPRESS02;Database=SpendWiseDb;Integrated Security=true")]
    [InlineData("ConnectionStrings:DefaultConnection", "Server=DESKTOP-B5KA3LT\\SQLEXPRESS01;Database=OtherDb;Integrated Security=true")]
    [InlineData("Jwt:Key", "short")]
    [InlineData("Jwt:Key", "")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Kestrel:Endpoints:External:Url", "http://0.0.0.0:5159")]
    public void AdminE2E_rejects_wrong_target_weak_JWT_or_external_binding(string key, string value)
    {
        var builder = Builder("AdminE2E");
        builder.Configuration[key] = value;
        Assert.Throws<InvalidOperationException>(() => Configure(builder));
    }

    [Fact]
    public void AdminE2E_opt_in_adds_only_scoped_auth_worker_and_fails_closed_without_scope()
    {
        var builder = Builder("AdminE2E");
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Configuration["AuthRecoveryDelivery:Enabled"] = "true";
        Assert.Throws<InvalidOperationException>(() => Configure(builder));
        builder.Configuration["AuthRecoveryDelivery:NotBeforeUtc"] = "2026-10-16T00:00:00Z";
        Assert.Throws<InvalidOperationException>(() => Configure(builder));
        builder.Configuration["AuthRecoveryDelivery:AllowedRecipients:0"] = "admin@fixture.invalid";
        Assert.Throws<InvalidOperationException>(() => Configure(builder));
        builder.Configuration["Email:DeliveryMode"]="Smtp";
        builder.Configuration["Email:Smtp:Host"]="smtp.fixture.invalid";
        builder.Configuration["Email:Smtp:From"]="sender@fixture.invalid";
        Configure(builder);
        var workers = builder.Services.Where(x=>x.ServiceType==typeof(IHostedService)).ToArray();
        Assert.Equal(2, workers.Length);
        Assert.Single(workers,x=>x.ImplementationType==typeof(SpendWise.Infrastructure.Notifications.AuthRecoveryEmailBackgroundService));
        Assert.DoesNotContain(workers,x=>x.ImplementationType==typeof(SpendWise.Infrastructure.Notifications.EmailOutboxBackgroundService));
        Assert.Equal("http://localhost:3000",builder.Configuration["Email:WebsiteUrl"]);
    }

    private static WebApplicationBuilder Builder(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        { EnvironmentName = environment, ContentRootPath = Path.GetTempPath() });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = @"Server=DESKTOP-B5KA3LT\SQLEXPRESS01;Database=SpendWiseDb;Integrated Security=true",
            ["Jwt:Key"] = new string('t', 64), ["Jwt:Issuer"] = "test-issuer", ["Jwt:Audience"] = "test-audience"
        });
        return builder;
    }

    private static void Configure(WebApplicationBuilder builder)
    {
        AdminE2EStartup.ConfigureRuntime(builder);
    }
}
