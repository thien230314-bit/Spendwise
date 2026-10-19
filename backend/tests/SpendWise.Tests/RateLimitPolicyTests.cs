using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.API.Controllers;
using Xunit;

namespace SpendWise.Tests;

// Test metadata giữ financial limiter trên thao tác ghi tiền nhưng không chặn route chỉ đọc.
public sealed class RateLimitPolicyTests
{
    [Fact]
    public void FinancialMutations_HaveFinancialPolicy()
    {
        // Arrange: liệt kê các action thực sự posting hoặc thay đổi trạng thái tiền.
        var actions = new (Type Controller, string Method)[]
        {
            (typeof(TransactionsController), nameof(TransactionsController.Income)),
            (typeof(TransactionsController), nameof(TransactionsController.Expense)),
            (typeof(TransfersController), nameof(TransfersController.Send)),
            (typeof(TransfersController), nameof(TransfersController.Internal)),
            (typeof(TransfersController), nameof(TransfersController.Confirm)),
            (typeof(GoalsController), nameof(GoalsController.Contribute)),
            (typeof(GoalsController), nameof(GoalsController.Withdraw)),
            (typeof(GroupsController), nameof(GroupsController.Contribute)),
            (typeof(GroupsController), nameof(GroupsController.CreateExpense)),
            (typeof(GroupsController), nameof(GroupsController.PayFundRequest)),
            (typeof(GroupsController), nameof(GroupsController.PayFundRound)),
            (typeof(GroupsController), nameof(GroupsController.CreateRefundPayment)),
            (typeof(BankConnectionsController), nameof(BankConnectionsController.Link)),
            (typeof(BankConnectionsController), nameof(BankConnectionsController.Unlink)),
            (typeof(BankConnectionsController), nameof(BankConnectionsController.Reactivate)),
            (typeof(BankConnectionsController), nameof(BankConnectionsController.Sync)),
            (typeof(BankConnectionsController), nameof(BankConnectionsController.StartTestModeLink)),
            (typeof(BankConnectionsController), nameof(BankConnectionsController.ConfirmTestModeLink)),
            (typeof(BankConnectionsController), nameof(BankConnectionsController.CreateTestModeTopUp)),
            (typeof(BankConnectionsController), nameof(BankConnectionsController.ConfirmTestModeTopUp))
        };

        // Act + Assert: mỗi financial action mang đúng policy name.
        foreach (var action in actions)
        {
            var attribute = action.Controller
                .GetMethod(action.Method)!
                .GetCustomAttribute<EnableRateLimitingAttribute>();
            Assert.NotNull(attribute);
            Assert.Equal("financial", attribute.PolicyName);
        }
    }

    [Fact]
    public void ReadOnlyNavigation_DoesNotUseFinancialPolicy()
    {
        // Arrange: các action GET đại diện cho ví, giao dịch, group và dữ liệu ngân hàng ngoài.
        var actions = new (Type Controller, string Method)[]
        {
            (typeof(TransactionsController), nameof(TransactionsController.Search)),
            (typeof(TransfersController), nameof(TransfersController.Get)),
            (typeof(GroupsController), nameof(GroupsController.Expenses)),
            (typeof(GroupsController), nameof(GroupsController.FundTransactions)),
            (typeof(BankConnectionsController), nameof(BankConnectionsController.List)),
            (typeof(BankTransactionsController), nameof(BankTransactionsController.List))
        };

        // Act + Assert: limiter không được gắn ở action hoặc cả Controller chứa action đọc.
        foreach (var action in actions)
        {
            Assert.Null(action.Controller.GetCustomAttribute<EnableRateLimitingAttribute>());
            Assert.Null(action.Controller
                .GetMethod(action.Method)!
                .GetCustomAttribute<EnableRateLimitingAttribute>());
        }
    }
}
