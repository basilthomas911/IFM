using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command.EventProjector;
public sealed class FuturesPositionEventProjector : ConventionalEventProjector<FuturesTradePositionCommandActor>
{
    readonly IFuturesPositionCommandContext context;
    readonly ImmutableArray<EventProjectionDescriptor> descriptors;
    public FuturesPositionEventProjector(ICommandActorContext<FuturesTradePositionCommandActor> actorContext, EventProjectorReliabilityOptions? options = null) : base(Typed(actorContext).DurableReplayQueue, Typed(actorContext).DbEventSource, Typed(actorContext).BlackboardService, Typed(actorContext).Logger, options)
    {
        context = Typed(actorContext);
        descriptors = [DescribeNotification<FuturesPositionChangedEvent, StrategyPositionId>(ProjectAsync).WithReadModelRebuild((source, _) => PositionProjectionActions.RebuildReadModelAsync(context.DbFactory, (FuturesPositionChangedEvent)source)), DescribeSnapshot<FuturesTradePlanUpdatedEvent, FuturesTradePlanId>(ProjectTradePlanAsync)];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(FuturesPositionChangedEvent), typeof(FuturesTradePlanUpdatedEvent)];

    Task ProjectAsync(FuturesPositionChangedEvent changed) => PositionProjectionActions.ProjectAsync(context, context.DbFactory, changed, FuturesTradePositionCommandActor.ActorName, FuturesRealtimeActor.ActorName, FuturesTradePositionRealtimeActor.ActorName);
    Task ProjectTradePlanAsync(FuturesTradePlanUpdatedEvent updated, CancellationToken cancellationToken) =>
        string.Equals(context.DbFactory.TradePlanDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
            ? Task.CompletedTask // Required rows already committed with the source event.
            : context.DbFactory.TradePlanDb.DbWriter.ProjectMaterialAsync(updated.Plan, cancellationToken);
    static IFuturesPositionCommandContext Typed(ICommandActorContext<FuturesTradePositionCommandActor> context) => context as IFuturesPositionCommandContext ?? throw new ArgumentException("Typed Futures position context required.", nameof(context));
}
