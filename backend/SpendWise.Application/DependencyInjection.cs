using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Transactions.Commands;
using SpendWise.Application.Features.Budgets;
using SpendWise.Application.Features.Goals.Commands;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.AiAssistant;
using SpendWise.Application.AI;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Banking;
using SpendWise.Application.Features.Banking;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Evidence;
using SpendWise.Application.Features.BankDirectPayments;
using SpendWise.Application.Features.Friends;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Features.Notifications;
using SpendWise.Application.Features.AdminAi;

namespace SpendWise.Application;

// Lớp này đăng ký MediatR cùng các thành phần điều phối nghiệp vụ của Application.

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Đăng ký toàn bộ MediatR handler từ assembly Application.
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(
                typeof(ApplicationAssemblyReference).Assembly));
        services.AddScoped<AuthenticationSupport>();
        services.AddScoped<LoginChallengeSupport>();
        services.AddScoped<AccountCredentialLockout>();
        services.AddScoped<SecurityPinResetSupport>();
        services.AddScoped<EmailVerificationSupport>();
        services.AddScoped<InteractiveLoginSupport>();
        services.AddScoped<ManualTransactionCreator>();
        services.AddScoped<BudgetReader>();
        services.AddScoped<GoalMovementExecutor>();
        services.AddScoped<GroupAccess>();
        services.AddScoped<SpendWise.Application.Features.V1Groups.V1FundService>();
        services.AddScoped<WalletFundAccounting>();
        services.AddScoped<IAiDataTools, AiFinancialContextBuilder>();
        services.AddScoped<IAiToolRegistry, AiToolRegistry>();
        services.AddScoped<AdminAiContextBuilder>();
        services.AddScoped<IBankTransactionIngestionService, BankTransactionIngestionService>();
        services.AddScoped<AdminAuditWriter>();
        services.AddScoped<AdminSecurityGuard>();
        services.AddScoped<FinancialSecurityGuard>();
        services.AddScoped<EvidenceSupport>();
        services.AddScoped<BankDirectPaymentSupport>();
        services.AddScoped<IBankDirectPaymentReconciler, BankDirectPaymentReconciler>();
        services.AddScoped<SearchFriendContactQueryHandler>();
        services.AddScoped<GroupDeletionSupport>();
        services.AddScoped<CaseAttachmentValidator>();
        services.AddScoped<SupportCaseEmailSupport>();
        services.AddScoped<BudgetAlertEvaluator>();
        services.AddScoped<FinancialAlertEvaluator>();

        return services;
    }
}
