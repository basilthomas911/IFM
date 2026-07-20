using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Fund.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to change the state of a fund order trade, including the trade identifier, new state, and an associated error code.
/// </summary>
/// <param name="FundOrderTradeId">The fund order trade identifier whose state is to be changed. Cannot be null.</param>
/// <param name="TradeState">The new trade state to apply.</param>
/// <param name="ErrorCode">The error code associated with the change trade state operation. Used to indicate specific error conditions or statuses.</param>
public record ChangeFundOrderTradeStateParameter(FundOrderTradeId FundOrderTradeId, TradeState TradeState, int ErrorCode)
    : ICommandParameter
{
}
