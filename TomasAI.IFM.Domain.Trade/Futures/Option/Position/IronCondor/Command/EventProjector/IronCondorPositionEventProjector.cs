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
public sealed class IronCondorPositionEventProjector : ConventionalEventProjector<FuturesIronCondorTradePositionCommandActor>
{
    readonly IIronCondorPositionCommandContext context;
    readonly ImmutableArray<EventProjectionDescriptor> descriptors;
    public IronCondorPositionEventProjector(ICommandActorContext<FuturesIronCondorTradePositionCommandActor> actorContext, EventProjectorReliabilityOptions? options = null) : base(Typed(actorContext).DurableReplayQueue, Typed(actorContext).DbEventSource, Typed(actorContext).BlackboardService, Typed(actorContext).Logger, options)
    {
        context = Typed(actorContext);
        descriptors = [DescribeNotification<IronCondorMonitoringInitializedEvent, StrategyPositionId>(source => string.Equals(context.DbFactory.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? Task.CompletedTask : ProjectMonitoringLimitsAsync(source)).WithReadModelRebuild((source, _) => ProjectMonitoringLimitsAsync((IronCondorMonitoringInitializedEvent)source)), new EventProjectionDescriptor(typeof(IronCondorPositionChangedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation, async (source, _) =>
        {
            await ProjectAsync((IronCondorPositionChangedEvent)source).ConfigureAwait(false);
            return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
        }, _ => null, (_, _) => null, publishProcessingEvent: false, publishTerminalEvent: false).WithReadModelRebuild((source, _) => PositionProjectionActions.RebuildReadModelAsync(context.DbFactory, (IronCondorPositionChangedEvent)source)), DescribeSnapshot<IronCondorTradePlanUpdatedEvent, TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId>(ProjectTradePlanAsync)];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(IronCondorPositionChangedEvent), typeof(IronCondorMonitoringInitializedEvent), typeof(IronCondorTradePlanUpdatedEvent)];

    /// <inheritdoc/>
    protected override bool ShouldPublishProjectionEvent(TomasAI.IFM.Shared.EventSourcing.IEvent domainEvent)
        => domainEvent is not IronCondorTradePlanUpdatedEvent plan || plan.Plan.MaterialChange;

    /// <summary>Projects history without replaying market-mark UI/plan notifications; financial boundaries keep their existing lifecycle.</summary>
    /// <param name = "changed">The committed position event.</param><returns>The history or financial projection operation.</returns>
    async Task ProjectAsync(IronCondorPositionChangedEvent changed)
    {
        if (changed.PositionSnapshot.Phase == StrategyPositionPhase.MarkToMarket)
        {
            // This mark was already delivered to the live view.
            // Recovery retains the history without changing the live view.
            if (!string.Equals(context.DbFactory.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
                await context.DbFactory.TradeDb.UpsertStrategyPositionAsync(changed.PositionSnapshot).ConfigureAwait(false);
            return;
        }

        await context.SendAsync<IronCondorPositionChangedEvent, StrategyPositionId>(changed with { Subject = new(TomasAI.IFM.Shared.EventModelActor.ActorType.Event, "FuturesIronCondorTradePositionEvent", IronCondorPositionChangedEvent.Verb, changed.EntityId.Format()) }).ConfigureAwait(false);
        await PositionProjectionActions.ProjectAsync(context, context.DbFactory, changed, FuturesIronCondorTradePositionCommandActor.ActorName, FuturesOptionRealtimeActor.ActorName, IronCondorTradePositionRealtimeActor.ActorName).ConfigureAwait(false);
    }

    /// <summary>Rebuilds accepted limit configuration from its recorded source facts; live PostgreSQL notification already skips this write.</summary>
    Task ProjectMonitoringLimitsAsync(IronCondorMonitoringInitializedEvent initialized) => context.DbFactory.TradeDb.ProjectMonitoringLimitsAsync(initialized.TradeLimits, initialized.SpreadLimits);
    Task ProjectTradePlanAsync(IronCondorTradePlanUpdatedEvent updated, CancellationToken cancellationToken) => string.Equals(context.DbFactory.TradePlanDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? Task.CompletedTask // Required rows already committed with the source event.
 : context.DbFactory.TradePlanDb.DbWriter.ProjectMaterialAsync(updated.Plan, cancellationToken);
    static IIronCondorPositionCommandContext Typed(ICommandActorContext<FuturesIronCondorTradePositionCommandActor> actorContext) => actorContext as IIronCondorPositionCommandContext ?? throw new ArgumentException("Typed Iron Condor position context required.", nameof(actorContext));
}
