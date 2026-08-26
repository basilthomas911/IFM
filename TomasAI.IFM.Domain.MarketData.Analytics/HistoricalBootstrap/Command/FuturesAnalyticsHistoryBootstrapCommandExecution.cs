using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command;

/// <summary>Translates a parameter-only bootstrap command into its durable Requested event.</summary>
public static class FuturesAnalyticsHistoryBootstrapCommandExecution
{
    /// <summary>Applies one new bootstrap request to event-sourced state.</summary>
    public static ServiceResult<GuidResult> Execute(
        this BootstrapFuturesAnalyticsHistoryCommand command,
        FuturesAnalyticsHistoryBootstrapCommandState state)
    {
        if (state.IsRequested)
            return command.UpdateFailed("The bootstrap attempt was already requested.");
        var updated = state.Update(new FuturesAnalyticsHistoryBootstrapRequestedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesAnalyticsHistoryBootstrapRequestedEvent.Actor,
                FuturesAnalyticsHistoryBootstrapRequestedEvent.Verb,
                command.EntityId.Format()),
            EntityId = command.EntityId,
            Parameters = command.Parameters
        }, command);
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed("Unable to apply the bootstrap Requested event.");
    }
}
