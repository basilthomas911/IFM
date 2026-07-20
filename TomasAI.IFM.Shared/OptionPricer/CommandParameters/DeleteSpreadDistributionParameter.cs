using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer.CommandParameters;

/// <summary>
/// Represents the parameters required to delete paired put and call spread distributions for a specific trade context.
/// </summary>
/// <param name="TradeId">The trade identifier.</param>
/// <param name="ValueDate">The value date of the spread distribution.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record DeleteSpreadDistributionParameter(
    int TradeId,
    DateOnly ValueDate,
    TradeStatus TradeStatus, 
    int DaysToExpiry,
    int ErrorCode)
    : ICommandParameter;
