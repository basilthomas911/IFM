using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Realtime.Extensions;

/// <summary>Exposes the bar publisher Realtime actor's typed context as readonly extension properties.</summary>
public static class FuturesTradeSessionBarPublisherRealtimeExtensions
{
    extension(IRealtimeActorContext<FuturesTradeSessionBarPublisherRealtimeActor> context)
    {
        /// <summary>Gets the typed domain context.</summary>
        public IFuturesTradeSessionBarPublisherRealtimeContext DomainContext =>
            context as IFuturesTradeSessionBarPublisherRealtimeContext
            ?? throw new InvalidOperationException("The trade-session bar publisher requires its typed context.");
        /// <summary>Gets the actor-centric bar accumulation model.</summary>
        public FuturesTradeSessionBarAccumulator BarAccumulator => context.DomainContext.Accumulator;
        /// <summary>Gets the server clock.</summary>
        public TimeProvider TimeProvider => context.DomainContext.TimeProvider;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesTradeSessionBarPublisherRealtimeActor> Logger => context.DomainContext.Logger;
    }

    /// <summary>Sends one deterministic completed-bar publication command to the Command actor.</summary>
    public static async ValueTask<ServiceResult<GuidResult>> PublishFuturesTradeSessionBarAsync(
        this IEventActorContext context,
        FuturesTradeSessionBarReadModel bar)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(bar);
        var entityId = new FuturesTradeSessionBarEntityId(bar.MarketSeriesIdentity, bar.TimeFrame);
        var command = new PublishFuturesTradeSessionBarCommand
        {
            CommandId = bar.ObservationId.Value,
            Subject = new(
                ActorType.Command,
                PublishFuturesTradeSessionBarCommand.Actor,
                PublishFuturesTradeSessionBarCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            Bar = bar
        };
        var result = await context.RequestAsync<PublishFuturesTradeSessionBarCommand,
            FuturesTradeSessionBarEntityId>(command).ConfigureAwait(false);
        if (result?.Success != true)
            throw new InvalidOperationException(result?.ErrorMessage ?? "Bar publication command failed.");
        return result;
    }
}
