using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.EventProjector;
public sealed class VerticalSpreadPositionEventProjector : ConventionalEventProjector<FuturesVerticalSpreadTradePositionCommandActor>
{
    readonly IVerticalSpreadPositionCommandContext context;
    readonly ImmutableArray<EventProjectionDescriptor> descriptors;
    public VerticalSpreadPositionEventProjector(ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> actorContext, EventProjectorReliabilityOptions? options = null) : base(Typed(actorContext).DurableReplayQueue, Typed(actorContext).DbEventSource, Typed(actorContext).BlackboardService, Typed(actorContext).Logger, options)
    {
        context = Typed(actorContext);
        descriptors = [DescribeNotification<VerticalSpreadPositionChangedEvent, StrategyPositionId>(ProjectAsync).WithReadModelRebuild((source, _) => PositionProjectionActions.RebuildReadModelAsync(context.DbFactory, (VerticalSpreadPositionChangedEvent)source)), DescribeSnapshot<VerticalSpreadTradePlanUpdatedEvent, VerticalSpreadTradePlanId>(ProjectTradePlanAsync)];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(VerticalSpreadPositionChangedEvent), typeof(VerticalSpreadTradePlanUpdatedEvent)];

    Task ProjectAsync(VerticalSpreadPositionChangedEvent changed) => PositionProjectionActions.ProjectAsync(context, context.DbFactory, changed, FuturesVerticalSpreadTradePositionCommandActor.ActorName, FuturesOptionRealtimeActor.ActorName, VerticalSpreadTradePositionRealtimeActor.ActorName);
    Task ProjectTradePlanAsync(VerticalSpreadTradePlanUpdatedEvent updated, CancellationToken cancellationToken) =>
        string.Equals(context.DbFactory.TradePlanDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
            ? Task.CompletedTask // Required rows already committed with the source event.
            : context.DbFactory.TradePlanDb.DbWriter.ProjectMaterialAsync(updated.Plan, cancellationToken);
    static IVerticalSpreadPositionCommandContext Typed(ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> actorContext) => actorContext as IVerticalSpreadPositionCommandContext ?? throw new ArgumentException("Typed Vertical Spread position context required.", nameof(actorContext));
}
