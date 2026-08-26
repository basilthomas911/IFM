using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command;

/// <summary>Translates a parameter-only data load command into its durable Requested event.</summary>
public static class FuturesAnalyticsHistoricalDataLoaderCommandExecution
{
    /// <summary>Applies one new data load request to event-sourced state.</summary>
    public static ServiceResult<GuidResult> Execute(
        this LoadFuturesAnalyticsHistoricalDataCommand command,
        FuturesAnalyticsHistoricalDataLoaderCommandState state)
    {
        if (state.IsRequested)
            return command.UpdateFailed("The data load attempt was already requested.");
        var updated = state.Update(new FuturesAnalyticsHistoricalDataLoaderRequestedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Actor,
                FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Verb,
                command.EntityId.Format()),
            EntityId = command.EntityId,
            Parameters = command.Parameters
        }, command);
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed("Unable to apply the data load Requested event.");
    }
}
