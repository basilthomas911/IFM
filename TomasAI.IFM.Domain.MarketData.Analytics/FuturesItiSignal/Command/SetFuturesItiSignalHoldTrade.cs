using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;

public static class SetFuturesItiSignalHoldTrade
{
    /// <summary>
    /// Handle a <see cref="SetFuturesItiSignalHoldTradeCommand"/> by setting the hold-trade state
    /// and producing the corresponding <see cref="FuturesItiSignalHoldTradeSetEvent"/>.
    /// </summary>
    /// <param name="e">The set hold-trade command to execute.</param>
    /// <param name="state">The current actor command state.</param>
    /// <returns>A <see cref="ServiceResult{GuidResult}"/> indicating whether the state was successfully updated.</returns>
    public static ServiceResult<GuidResult> Execute(this SetFuturesItiSignalHoldTradeCommand e, FuturesItiSignalCommandState state)
    {
        var updated = state.Exists(e.EntityId) && state.IsTradeInReadyState && state.Update(e.CreateSetHoldTradeEvent(state), e);
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(e.CommandId))
            : e.UpdateFailed($"{e.CommandName}: unable to set hold-trade state");
    }

    /// <summary>
    /// Creates a futures ITI signal generated event for setting hold-trade state.
    /// </summary>
    public static FuturesItiSignalHoldTradeSetEvent CreateSetHoldTradeEvent(
        this SetFuturesItiSignalHoldTradeCommand command,
        FuturesItiSignalCommandState state) => new()
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesItiSignalHoldTradeSetEvent.Actor,
                FuturesItiSignalHoldTradeSetEvent.Verb,
                command.EntityId.Format()),
            EntityId = command.EntityId,
            FuturesItiSignal = state.CurrentSignal! with
            {
                ContractId = command.ContractId,
                ValueDate = command.ValueDate,
                SequenceId = 0,
                IntrinsicTime = command.Timestamp,
                IntrinsicTimeLength = 0,
                IntrinsicTimeMode = IntrinsicTimeModeType.HoldTradeChanged,
                TradeState = IntrinsicTimeTradeState.Hold
            },
            CreatedOn = command.OriginatedOn,
            CreatedBy = command.OriginatedBy
        };

}
