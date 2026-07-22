using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command;

public static class UpdateFuturesTradeSignal
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static bool Execute(this UpdateFuturesTradeSignalCommand e, FuturesTradeSignalCommandState state)
       => e.Compute(out var model) switch
       {
           _ when state.HasFuturesTradeSignalChanged(model.FuturesTradeSignal)
               => state.Update(e.CreateFuturesTradeSignalUpdatedEvent(model), e),
           _ when state.HasFuturesItiSignalHoldTradeChanged(model.FuturesTradeSignal)
               => state.Update(e.CreateFuturesItiSignalHoldTradeChangedEvent(model), e),
           _ => false
       };

    /// <summary>
    /// Attempts to create a new futures trade signal compute model based on the specified command.
    /// </summary>
    /// <param name="e">The command containing the EOD data and technical signals used to generate the trade signal compute model.</param>
    /// <param name="computeModel">When this method returns, contains the resulting futures trade signal compute model if the operation succeeds;
    /// otherwise, contains null.</param>
    /// <returns>true if the compute model was successfully created; otherwise, false.</returns>
    internal static bool Compute(this UpdateFuturesTradeSignalCommand e, out FuturesTradeSignalCompute computeModel)
        => FuturesTradeSignalCompute.Create(e, out computeModel);

    /// <summary>
    /// Creates a new <see cref="FuturesTradeSignalUpdatedEvent"/> using the specified command and computed trade signal.
    /// </summary>
    internal static FuturesTradeSignalUpdatedEvent CreateFuturesTradeSignalUpdatedEvent(this UpdateFuturesTradeSignalCommand e, FuturesTradeSignalCompute computed)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesTradeSignalUpdatedEvent.Actor,
                FuturesTradeSignalUpdatedEvent.Verb,
                e.EntityId.Format()),
            EntityId = e.EntityId,
            FuturesTradeSignal = computed.FuturesTradeSignal,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };

    /// <summary>
    /// Creates a new <see cref="FuturesItiSignalHoldTradeChangedEvent"/> using the specified command and computed trade signal.
    /// </summary>
    internal static FuturesItiSignalHoldTradeChangedEvent CreateFuturesItiSignalHoldTradeChangedEvent(this UpdateFuturesTradeSignalCommand e, FuturesTradeSignalCompute computed)
    {
        var entityId = new FuturesItiSignalEntityId(
            e.EntityId.ContractId,
            e.EntityId.ValueDate,
            e.EntityId.TimePeriod);
        var signalId = FuturesItiSignalId.Create(
            e.EntityId.ContractId,
            e.EntityId.ValueDate,
            e.EntityId.TimePeriod,
            e.OriginatedOn);
        return new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesItiSignalHoldTradeChangedEvent.Actor,
                FuturesItiSignalHoldTradeChangedEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            FuturesItiSignalId = signalId,
            HoldTrade = computed.FuturesTradeSignal.TradeExecuteState == TradeExecuteState.Hold,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };
    }


}
