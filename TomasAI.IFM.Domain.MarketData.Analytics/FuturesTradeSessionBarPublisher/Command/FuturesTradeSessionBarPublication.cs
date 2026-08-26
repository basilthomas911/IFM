using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command;

/// <summary>Applies a validated bar publication command to event-sourced publisher state.</summary>
public static class FuturesTradeSessionBarPublication
{
    /// <summary>Creates and applies the durable Published event for one completed bar.</summary>
    public static ServiceResult<GuidResult> Execute(
        this PublishFuturesTradeSessionBarCommand command,
        FuturesTradeSessionBarPublisherCommandState state)
    {
        if (state.LastPublishedBarId == command.Bar.ObservationId)
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var updated = state.Update(new FuturesTradeSessionBarPublishedEvent
        {
            Subject = new(
                ActorType.Event,
                FuturesTradeSessionBarPublishedEvent.Actor,
                FuturesTradeSessionBarPublishedEvent.Verb,
                command.EntityId.Format()),
            EntityId = command.EntityId,
            Bar = command.Bar
        }, command);
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed("Unable to apply the futures trade-session bar Published event.");
    }
}
