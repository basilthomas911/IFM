using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to remove an order from a fund, including the fund order identifier and an associated error code.
/// </summary>
/// <param name="FundOrderId">The fund order identifier to be removed. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the remove order operation. Used to indicate specific error conditions or statuses.</param>
public record RemoveOrderFromFundParameter(FundOrderId FundOrderId, int ErrorCode)
    : ICommandParameter
{
}
