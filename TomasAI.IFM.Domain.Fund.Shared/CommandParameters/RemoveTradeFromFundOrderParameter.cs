using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to remove a trade from a fund order, including the fund order trade identifier and an associated error code.
/// </summary>
/// <param name="FundOrderTradeId">The fund order trade identifier to be removed. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the remove trade operation. Used to indicate specific error conditions or statuses.</param>
public record RemoveTradeFromFundOrderParameter(FundOrderTradeId FundOrderTradeId, int ErrorCode)
    : ICommandParameter
{
}
