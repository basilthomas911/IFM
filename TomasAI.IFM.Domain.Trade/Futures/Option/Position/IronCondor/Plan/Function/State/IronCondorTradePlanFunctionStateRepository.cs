using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;

/// <summary>Loads one latest source snapshot, commits the new snapshot, then submits its Scylla projection once.</summary>
/// <remarks>No history is replayed or cached. Source events and Scylla history may differ when a projection is dropped.</remarks>
public sealed class IronCondorTradePlanFunctionStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<FuturesIronCondorTradePositionCommandActor> projector,
    ILogger<IronCondorTradePlanFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceFunctionStateRepository<IronCondorTradePlanFunctionState, UpdateIronCondorTradePlanCommand>
{
    /// <summary>Restores only the last full snapshot in the source stream; it never queries Scylla or loads earlier plans.</summary>
    /// <param name="request">The current position and captured calculation inputs.</param>
    /// <param name="cancellationToken">Cancels the single-snapshot load.</param>
    /// <returns>A request-local state initialized from at most one persisted snapshot event.</returns>
    public async ValueTask<IronCondorTradePlanFunctionState> LoadStateAsync(UpdateIronCondorTradePlanCommand request,
        CancellationToken cancellationToken = default)
    {
        var state = new IronCondorTradePlanFunctionState { Id = request.Subject.ThreadId };
        var streamId = await GetStreamId(request.StreamId, cancellationToken).ConfigureAwait(false);
        await EventSourceDb.MapReduceActorEventStreamAsync<IronCondorTradePlanFunctionState, IronCondorTradePlanUpdatedEvent>(
            streamId, 1, state.ReplayEvents, cancellationToken).ConfigureAwait(false);
        return state.Prepare(request);
    }

    /// <summary>Commits the snapshot event before attempting its projection; projection failures are logged and dropped.</summary>
    /// <param name="context">The function context.</param>
    /// <param name="state">The current request's state and pending snapshot event.</param>
    /// <param name="request">The originating position update.</param>
    /// <param name="cancellationToken">Cancels source persistence before commit.</param>
    /// <returns>The source commit and single projection submission. Projection failure does not undo the source event.</returns>
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, IronCondorTradePlanFunctionState state,
        UpdateIronCondorTradePlanCommand request, CancellationToken cancellationToken = default)
    {
        var committed = await SaveStateEventsAsync(state, request, state.CommittedStreamVersion,
            cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
        try { await projector.DomainEventsProjectionAsync(committed).ConfigureAwait(false); }
        catch (Exception error)
        {
            logger.LogError(error,
                "Trade plan projection dropped after source commit; MethodName={MethodName} CommandId={CommandId} TradeId={TradeId} ValueDate={ValueDate} EventId={EventId} PlanRevision={PlanRevision}",
                nameof(SaveCompletedStateAsync), request.CommandId, request.EntityId.Position.Trade.Format(),
                request.EntityId.ValueDate, state.CompletedEvent?.EventId, state.LatestPlanRevision);
        }
    }

    /// <inheritdoc />
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => ValueTask.CompletedTask;
}
