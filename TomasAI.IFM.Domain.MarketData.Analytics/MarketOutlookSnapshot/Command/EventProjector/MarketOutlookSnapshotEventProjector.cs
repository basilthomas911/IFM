using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.EventProjector;

/// <summary>
/// Projects committed Market Outlook checkpoints to ScyllaDB and publishes terminal events to the realtime bridge.
/// </summary>
public sealed class MarketOutlookSnapshotEventProjector
    : ConventionalEventProjector<MarketOutlookSnapshotCommandActor>
{
    readonly ICommandActorContext<MarketOutlookSnapshotCommandActor> _actorContext;
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors;

    /// <summary>Initializes the conventional Market Outlook projector.</summary>
    public MarketOutlookSnapshotEventProjector(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> actorContext,
        EventProjectorReliabilityOptions? reliabilityOptions = null)
        : base(
            actorContext.DurableReplayQueue,
            actorContext.DbEventSource,
            actorContext.BlackboardService,
            actorContext.Logger,
            reliabilityOptions)
    {
        _actorContext = actorContext;
        _descriptors =
        [
            DescribeRealtimeTerminal<
                MarketOutlookComponentObservedEvent,
                MarketOutlookComponentObservedCompleteEvent,
                MarketOutlookComponentObservedFailEvent,
                MarketOutlookEntityId>((observed, cancellationToken) => actorContext.DbFactory.MarketDataDb
                    .UpsertMarketOutlookWorkingStateAsync(observed.WorkingState, cancellationToken)),
            DescribeRealtimeTerminal<
                MarketOutlookSnapshotPublishedEvent,
                MarketOutlookSnapshotPublishedCompleteEvent,
                MarketOutlookSnapshotPublishedFailEvent,
                MarketOutlookEntityId>(async (published, cancellationToken) =>
                {
                    await actorContext.DbFactory.MarketDataDb
                        .UpsertMarketOutlookWorkingStateAsync(published.WorkingState, cancellationToken)
                        .ConfigureAwait(false);
                    await actorContext.DbFactory.MarketDataDb
                        .UpsertMarketOutlookSnapshotAsync(published.MarketOutlook, cancellationToken)
                        .ConfigureAwait(false);
                })
        ];
    }

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;

    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();

    EventProjectionDescriptor DescribeRealtimeTerminal<TEvent, TComplete, TFail, TEntityId>(
        Func<TEvent, CancellationToken, Task> applyAsync)
        where TEvent : class, IEvent<TEntityId>
        where TComplete : class, ICompleteEvent<TEntityId>
        where TFail : class, IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
        => new(
            typeof(TEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, executionContext) =>
            {
                var source = (TEvent)domainEvent;
                try
                {
                    await applyAsync(source, executionContext.CancellationToken).ConfigureAwait(false);
                    var completed = source.ToCompleteEvent<TComplete, TEntityId>();
                    await _actorContext.SendAsync<TComplete, TEntityId>(
                        (TComplete)completed,
                        executionContext.CancellationToken).ConfigureAwait(false);
                    return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
                }
                catch (Exception exception)
                {
                    var failed = source.ToFailEvent<TFail, TEntityId>(exception);
                    await _actorContext.SendAsync<TFail, TEntityId>(
                        (TFail)failed,
                        executionContext.CancellationToken).ConfigureAwait(false);
                    throw;
                }
            },
            _ => null,
            (_, _) => null,
            useDurableReplay: true,
            publishTerminalEvent: false);
}
