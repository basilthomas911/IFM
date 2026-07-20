using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to add an order to a fund, including the fund order details and an associated error code.
/// </summary>
/// <param name="FundOrder">The fund order details to be added. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the add order operation. Used to indicate specific error conditions or statuses.</param>
public record AddOrderToFundParameter(FundOrderReadModel FundOrder, int ErrorCode)
    : ICommandParameter
{
}