using SpendWise.Application.Groups;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Groups;

// Lớp này chuyển quỹ hiện tại của Group sang DTO.

internal static class GroupMappings
{
    public static SharedFundDto ToFundDto(SharedFund fund)
    {
        // Trả Id, GroupId, số dư và đơn vị tiền của SharedFund.
        return new SharedFundDto(fund.Id, fund.GroupId, fund.Balance, fund.Currency);
    }

}
