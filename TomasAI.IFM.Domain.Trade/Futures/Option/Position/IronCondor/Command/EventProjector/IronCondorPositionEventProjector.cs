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
            DescribeNotification<IronCondorMonitoringInitializedEvent, StrategyPositionId>(ProjectMonitoringLimitsAsync),
            new(typeof(IronCondorPositionChangedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
                async (source, _) =>
                {
                    await ProjectAsync((IronCondorPositionChangedEvent)source).ConfigureAwait(false);
                    return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
                }, _ => null, (_, _) => null, publishProcessingEvent: false, publishTerminalEvent: false),
            DescribeSnapshot<IronCondorTradePlanUpdatedEvent,
                TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId>(ProjectTradePlanAsync)
        ];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        [typeof(IronCondorPositionChangedEvent), typeof(IronCondorMonitoringInitializedEvent), typeof(IronCondorTradePlanUpdatedEvent)];

    /// <summary>Projects history without replaying market-mark UI/plan notifications; financial boundaries keep their existing lifecycle.</summary>
    /// <param name="changed">The committed position event.</param><returns>The history or financial projection operation.</returns>
    async Task ProjectAsync(IronCondorPositionChangedEvent changed)
    {
        if (changed.PositionSnapshot.Phase == StrategyPositionPhase.MarkToMarket)
        {
            // Source-commit publication already delivered this mark. Recovery only projects its history;
            // it must never regenerate an old live plan or overwrite a live view with replayed marks.
            await context.DbFactory.TradeDb.UpsertStrategyPositionAsync(changed.PositionSnapshot).ConfigureAwait(false);
            return;
        }
        await context.SendAsync<IronCondorPositionChangedEvent, StrategyPositionId>(changed with
        { Subject = new(TomasAI.IFM.Shared.EventModelActor.ActorType.Event, "FuturesIronCondorTradePositionEvent", IronCondorPositionChangedEvent.Verb, changed.EntityId.Format()) }).ConfigureAwait(false);
        await PositionProjectionActions.ProjectAsync(context, context.DbFactory, changed,
            FuturesIronCondorTradePositionCommandActor.ActorName, FuturesOptionRealtimeActor.ActorName,
            IronCondorTradePositionRealtimeActor.ActorName).ConfigureAwait(false);
    }

    /// <summary>Projects accepted limit configuration to its legacy tables, separate from disposable plan history.</summary>
    Task ProjectMonitoringLimitsAsync(IronCondorMonitoringInitializedEvent initialized) => Task.WhenAll(
        context.DbFactory.TradeDb.InsertTradeLimitAsync(initialized.TradeLimits),
        context.DbFactory.TradeDb.InsertTradeTypeLimitsAsync((ICollection<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel>)initialized.SpreadLimits));

    Task ProjectTradePlanAsync(IronCondorTradePlanUpdatedEvent updated, CancellationToken cancellationToken) =>
        context.DbFactory.TradePlanDb.DbWriter.ProjectMaterialAsync(updated.Plan, cancellationToken);

    static IIronCondorPositionCommandContext Typed(
        ICommandActorContext<FuturesIronCondorTradePositionCommandActor> actorContext) =>
        actorContext as IIronCondorPositionCommandContext ??
        throw new ArgumentException("Typed Iron Condor position context required.");
}
