using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;

public static class SetFuturesItiSignalHoldTrade
{
    /// <summary>
    /// Handle a <see cref="SetFuturesItiSignalHoldTradeCommand"/> by setting the hold-trade state
    /// and producing the corresponding <see cref="FuturesItiSignalGeneratedEvent"/>.
    /// </summary>
    /// <param name="e">The set hold-trade command to execute.</param>
    /// <param name="state">The current actor command state.</param>
    /// <returns><see langword="true"/> if the state was successfully updated; otherwise, <see langword="false"/>.</returns>
    public static bool Execute(this SetFuturesItiSignalHoldTradeCommand e, FuturesItiSignalCommandState state)
        => state.Exists(e.EntityId) && state.IsTradeInReadyState && state.Update(e.CreateSetHoldTradeEvent(state), e);

    /// <summary>
    /// Creates a futures ITI signal generated event for setting hold-trade state.
    /// </summary>
    public static FuturesItiSignalGeneratedEvent CreateSetHoldTradeEvent(this SetFuturesItiSignalHoldTradeCommand e, FuturesItiSignalCommandState state)
        => e.EntityId.CreateHoldTradeEvent(
            e.ContractId,
            e.ValueDate,
            e.Timestamp,
            state,
            IntrinsicTimeTradeState.Hold,
            e.OriginatedOn,
            e.OriginatedBy);

}
