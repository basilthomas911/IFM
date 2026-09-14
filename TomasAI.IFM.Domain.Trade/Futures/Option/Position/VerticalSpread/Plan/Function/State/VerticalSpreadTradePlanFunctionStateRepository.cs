using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;

public sealed class VerticalSpreadTradePlanFunctionStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<FuturesVerticalSpreadTradePositionCommandActor> projector,
    ILogger<VerticalSpreadTradePlanFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceFunctionStateRepository<VerticalSpreadTradePlanFunctionState, UpdateVerticalSpreadTradePlanCommand>
{
    public async ValueTask<VerticalSpreadTradePlanFunctionState> LoadStateAsync(
        UpdateVerticalSpreadTradePlanCommand request, CancellationToken cancellationToken = default) =>
        (await LoadStateFromSnapshotAsync<VerticalSpreadTradePlanFunctionState, VerticalSpreadTradePlanUpdatedEvent>(
            request, cancellationToken).ConfigureAwait(false)).Prepare(request);

    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context,
        VerticalSpreadTradePlanFunctionState state, UpdateVerticalSpreadTradePlanCommand request,
        CancellationToken cancellationToken = default)
    {
        var committed = await SaveStateEventsAsync(state, request, state.LastPersistedEventId,
            cancellationToken).ConfigureAwait(false);
        if (state.CompletedEvent?.Plan.MaterialChange == true)
            await projector.DomainEventsProjectionAsync(committed).ConfigureAwait(false);
    }

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context,
        DomainEventCollection events) => ValueTask.CompletedTask;
}
