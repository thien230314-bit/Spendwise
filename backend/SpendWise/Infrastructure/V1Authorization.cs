using Microsoft.AspNetCore.Authorization;
using SpendWise.Application.Features.Administration;
using SpendWise.Domain.Identity;

namespace SpendWise.API.Infrastructure;

public sealed record PlatformPermissionRequirement(PlatformPermission Permission) : IAuthorizationRequirement;

public sealed class PlatformPermissionAuthorizationHandler(PlatformPermissionService permissions)
    : AuthorizationHandler<PlatformPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PlatformPermissionRequirement requirement)
    {
        if (await permissions.HasAsync(context.User, requirement.Permission, CancellationToken.None))
            context.Succeed(requirement);
    }
}

public static class V1Authorization
{
    public const string OperationsRead = "V1.OperationsRead";
    public const string ManageOrdinaryAdmins = "V1.ManageOrdinaryAdmins";
    public const string ManageMaintenance = "V1.ManageMaintenance";
    public const string ManageConfiguration = "V1.ManageConfiguration";

    public static IServiceCollection AddV1Authorization(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var election = services.AddOptions<SpendWise.Application.Features.V1Groups.V1ElectionPolicy>();
        if (configuration is not null) election.Bind(configuration.GetSection("V1:Elections"));
        var activity = services.AddOptions<SpendWise.Application.Features.V1Groups.V1ActivityPolicy>();
        if (configuration is not null) activity.Bind(configuration.GetSection("V1:Activity"));
        services.AddScoped<SpendWise.Application.Features.V1Groups.V1ActivityService>();
        services.AddScoped<SpendWise.Application.Features.V1Groups.IV1InactivityEvidence>(sp => sp.GetRequiredService<SpendWise.Application.Features.V1Groups.V1ActivityService>());
        services.AddScoped<PlatformPermissionService>();
        services.AddScoped<ScopedCaseProfileReader>();
        services.AddScoped<ScopedCaseTransactionReader>();
        services.AddScoped<ScopedCaseFundReader>();
        services.AddScoped<V1CaseAssignment>();
        services.AddScoped<V1CaseWorkflow>();
        services.AddScoped<AdminA1Service>();
        services.AddScoped<AdminA2Service>();
        services.AddScoped<AdminA3Service>();
        services.AddScoped<SpendWise.Application.Features.Operations.V1MaintenanceService>();
        var broadcasts = services.AddOptions<SpendWise.Application.Features.Operations.V1BroadcastPolicy>();
        if (configuration is not null) broadcasts.Bind(configuration.GetSection("V1:Broadcast"));
        services.AddScoped<SpendWise.Application.Features.Operations.V1BroadcastService>();
        var reminders = services.AddOptions<SpendWise.Application.Features.Operations.V1ReminderPolicy>();
        if (configuration is not null) reminders.Bind(configuration.GetSection("V1:MaintenanceReminders"));
        services.AddScoped<SpendWise.Application.Features.Operations.V1MaintenanceReminders>();
        services.AddScoped<SpendWise.Application.Features.Operations.IV1MaintenanceSafety, SpendWise.Application.Features.Operations.UnconfiguredMaintenanceSafety>();
        services.AddScoped<OrdinaryAdminLifecycle>();
        services.AddScoped<IAuthorizationHandler, PlatformPermissionAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(OperationsRead, p => p.RequireAuthenticatedUser()
                .AddRequirements(new PlatformPermissionRequirement(PlatformPermission.OperationsRead)));
            options.AddPolicy(ManageOrdinaryAdmins, p => p.RequireAuthenticatedUser()
                .AddRequirements(new PlatformPermissionRequirement(PlatformPermission.ManageOrdinaryAdmins)));
            options.AddPolicy(ManageMaintenance, p => p.RequireAuthenticatedUser()
                .AddRequirements(new PlatformPermissionRequirement(PlatformPermission.ManageMaintenance)));
            options.AddPolicy(ManageConfiguration, p => p.RequireAuthenticatedUser()
                .AddRequirements(new PlatformPermissionRequirement(PlatformPermission.ManageConfiguration)));
        });
        return services;
    }
}
