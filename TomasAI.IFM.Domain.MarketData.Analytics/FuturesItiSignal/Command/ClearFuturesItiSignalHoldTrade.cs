using System;
using System.Collections.Generic;
using System.Text;
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
    /// and producing the corresponding <see cref="FuturesItiSignalGeneratedEvent"/>.
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
    internal static FuturesItiSignalGeneratedEvent CreateClearHoldTradeEvent(this ClearFuturesItiSignalHoldTradeCommand e, FuturesItiSignalCommandState state)
        => e.EntityId.CreateHoldTradeEvent( 
            e.ContractId,
            e.ValueDate,
            e.Timestamp,
            state,
            IntrinsicTimeTradeState.Ready,
            e.OriginatedOn,
            e.OriginatedBy);

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
    public static FuturesItiSignalGeneratedEvent CreateHoldTradeEvent(
        this FuturesItiSignalEntityId entityId,
        string contractId,
        DateOnly valueDate,
        DateTime timestamp,
        FuturesItiSignalCommandState state,
        IntrinsicTimeTradeState tradeState,
        DateTime createdOn,
        string createdBy)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor, FuturesItiSignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            FuturesItiSignal = state.CurrentSignal! with
            {
                ContractId = contractId,
                ValueDate = valueDate,
                SequenceId = 0,
                IntrinsicTime = timestamp,
                IntrinsicTimeLength = 0,
                IntrinsicTimeMode = IntrinsicTimeModeType.HoldTradeChanged,
                TradeState = tradeState
            },
            CreatedOn = createdOn,
            CreatedBy = createdBy
        };

}
