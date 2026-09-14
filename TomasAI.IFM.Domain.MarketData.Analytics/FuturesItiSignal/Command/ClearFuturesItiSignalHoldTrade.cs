using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;

public static class ClearFuturesItiSignalHoldTrade
{
    /// <summary>
    /// Handle a <see cref="ClearFuturesItiSignalHoldTradeCommand"/> by clearing the hold-trade state
    /// and producing the corresponding <see cref="FuturesItiSignalHoldTradeClearedEvent"/>.
    /// </summary>
    /// <param name="e">The clear hold-trade command to execute.</param>
    /// <param name="state">The current actor command state.</param>
    /// <returns>A <see cref="ServiceResult{GuidResult}"/> indicating whether the state was successfully updated.</returns>
    public static ServiceResult<GuidResult> Execute(this ClearFuturesItiSignalHoldTradeCommand e, FuturesItiSignalCommandState state)
    {
        var updated = state.Exists(e.EntityId) && state.IsTradeInHoldState && state.Update(e.CreateClearHoldTradeEvent(state), e);
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(e.CommandId))
            : e.UpdateFailed($"{e.CommandName}: unable to clear hold-trade state");
    }

    /// <summary>
    /// Creates a futures ITI signal generated event for clearing hold-trade state.
    /// </summary>
    internal static FuturesItiSignalHoldTradeClearedEvent CreateClearHoldTradeEvent(
        this ClearFuturesItiSignalHoldTradeCommand command,
        FuturesItiSignalCommandState state) => new()
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesItiSignalHoldTradeClearedEvent.Actor,
                FuturesItiSignalHoldTradeClearedEvent.Verb,
                command.EntityId.Format()),
            EntityId = command.EntityId,
            FuturesItiSignal = command.CreateHoldTradeSignal(
                state,
                IntrinsicTimeTradeState.Ready),
            CreatedOn = command.OriginatedOn,
            CreatedBy = command.OriginatedBy
        };

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="timestamp"></param>
    /// <param name="state"></param>
    /// <param name="tradeState"></param>
    /// <param name="createdOn"></param>
    /// <param name="createdBy"></param>
    /// <returns></returns>
    internal static FuturesItiSignalV2ReadModel CreateHoldTradeSignal(
        this ClearFuturesItiSignalHoldTradeCommand command,
        FuturesItiSignalCommandState state,
        IntrinsicTimeTradeState tradeState) =>
        state.CurrentSignal! with
            {
                ContractId = command.ContractId,
                ValueDate = command.ValueDate,
                SequenceId = 0,
                IntrinsicTime = command.Timestamp,
                IntrinsicTimeLength = 0,
                IntrinsicTimeMode = IntrinsicTimeModeType.HoldTradeChanged,
                TradeState = tradeState
            };

}
