using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.EventProjector;

public sealed class IronCondorPositionEventProjector
    : ConventionalEventProjector<FuturesIronCondorTradePositionCommandActor>
{
    readonly IIronCondorPositionCommandContext context;
    readonly ImmutableArray<EventProjectionDescriptor> descriptors;

    public IronCondorPositionEventProjector(
        ICommandActorContext<FuturesIronCondorTradePositionCommandActor> actorContext,
        EventProjectorReliabilityOptions? options = null)
        : base(Typed(actorContext).DurableReplayQueue, Typed(actorContext).DbEventSource,
            Typed(actorContext).BlackboardService, Typed(actorContext).Logger, options)
    {
        context = Typed(actorContext);
        descriptors =
        [
            DescribeNotification<IronCondorPositionChangedEvent, StrategyPositionId>(ProjectAsync),
            DescribeNotification<IronCondorTradePlanUpdatedEvent,
                TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId>(
                ProjectTradePlanAsync)
        ];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        [typeof(IronCondorPositionChangedEvent), typeof(IronCondorTradePlanUpdatedEvent)];

    Task ProjectAsync(IronCondorPositionChangedEvent changed) => PositionProjectionActions.ProjectAsync(
        context, context.DbFactory, changed, FuturesIronCondorTradePositionCommandActor.ActorName,
        FuturesOptionRealtimeActor.ActorName, IronCondorTradePositionRealtimeActor.ActorName);

    Task ProjectTradePlanAsync(IronCondorTradePlanUpdatedEvent updated) =>
        context.DbFactory.TradePlanDb.DbWriter.ProjectMaterialAsync(updated.Plan);

    static IIronCondorPositionCommandContext Typed(
        ICommandActorContext<FuturesIronCondorTradePositionCommandActor> actorContext) =>
        actorContext as IIronCondorPositionCommandContext ??
        throw new ArgumentException("Typed Iron Condor position context required.");
}
