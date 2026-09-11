using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;

public static class StartFuturesRsiSignal
{
    /// <summary>
    /// Handle a <see cref="StartFuturesRsiSignalCommand"/> by building the corresponding
    /// <see cref="FuturesRsiSignalStartedEvent"/> and updating the actor state.
    /// </summary>
    public static ServiceResult<GuidResult> Execute(this StartFuturesRsiSignalCommand e, FuturesRsiSignalCommandState state,IMarketSessionCalendar? calendar=null)
    {
        var seed=FuturesRsiHistoricalSeedModel.Decide(e,state.AccumulatorCheckpoint,calendar,DateTimeOffset.UtcNow);
        var checkpoint=state.AccumulatorCheckpoint;
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.FuturesRsiSignalReadModel? signal=null;
        foreach(var observation in seed.Observations)
        {
            var result=FuturesRsiWilderAccumulator.Apply(checkpoint,observation,e.EntityId.PeriodLength);
            checkpoint=result.Checkpoint;
            signal=FuturesRsiWilderSignalFactory.Create(observation,e.EntityId.PeriodLength,result);
        }
        if(!state.Update(e.CreateFuturesRsiSignalStartedEvent() with {HistoricalSeedCount=seed.Observations.Length,HistoricalSeedReason=seed.Reason,RestoredSignal=state.AccumulatorCheckpoint is null?null:state.FuturesRsiSignals.LastOrDefault()},e))return e.UpdateFailed("RSI start could not be applied.");
        // Publish only the final seed result; intermediate historical values must not race newer cache observations.
        if(signal is not null && !state.Update(new FuturesRsiSignalGeneratedEvent
        {
            Subject=new ActorSubject(ActorType.Event,FuturesRsiSignalGeneratedEvent.Actor,FuturesRsiSignalGeneratedEvent.Verb,e.EntityId.Format()),
            EntityId=e.EntityId,FuturesRsiSignal=signal,AccumulatorCheckpoint=checkpoint,CreatedBy=e.OriginatedBy,CreatedOn=e.OriginatedOn
        },e))return e.UpdateFailed("RSI historical checkpoint could not be applied.");
        return new ServiceOk<GuidResult>(new(e.CommandId));
    }

    internal static FuturesRsiSignalStartedEvent CreateFuturesRsiSignalStartedEvent(this StartFuturesRsiSignalCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalStartedEvent.Actor, FuturesRsiSignalStartedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            ValueDate = e.EntityId.ValueDate,
            StartedOn = e.OriginatedOn,
            StartedBy = e.OriginatedBy
        };

}
