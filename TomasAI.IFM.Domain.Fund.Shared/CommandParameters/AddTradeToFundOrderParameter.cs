using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to add a trade to a fund order, including the fund order trade details and an associated error code.
/// </summary>
/// <param name="FundOrderTrade">The fund order trade details to be added. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the add trade operation. Used to indicate specific error conditions or statuses.</param>
public record AddTradeToFundOrderParameter(FundOrderTradeReadModel FundOrderTrade, int ErrorCode)
    : ICommandParameter
{
}
