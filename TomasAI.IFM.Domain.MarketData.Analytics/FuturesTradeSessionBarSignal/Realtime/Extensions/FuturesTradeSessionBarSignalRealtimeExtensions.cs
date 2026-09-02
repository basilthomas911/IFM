using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Extensions;

/// <summary>Exposes the bar signal Realtime actor's typed context as readonly extension properties.</summary>
public static class FuturesTradeSessionBarSignalRealtimeExtensions
{
    extension(IRealtimeActorContext<FuturesTradeSessionBarSignalRealtimeActor> context)
    {
        /// <summary>Gets the typed domain context.</summary>
        public IFuturesTradeSessionBarSignalRealtimeContext DomainContext =>
            context as IFuturesTradeSessionBarSignalRealtimeContext
            ?? throw new InvalidOperationException("The trade-session bar signal requires its typed context.");
        /// <summary>Gets the actor-centric bar accumulation model.</summary>
        public FuturesTradeSessionBarAccumulatorRegistry BarAccumulators => context.DomainContext.Accumulators;
        /// <summary>Gets the server clock.</summary>
        public TimeProvider TimeProvider => context.DomainContext.TimeProvider;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesTradeSessionBarSignalRealtimeActor> Logger => context.DomainContext.Logger;
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
