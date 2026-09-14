using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;

public sealed class IronCondorTradePlanFunctionStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<FuturesIronCondorTradePositionCommandActor> projector,
    ILogger<IronCondorTradePlanFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceFunctionStateRepository<IronCondorTradePlanFunctionState, UpdateIronCondorTradePlanCommand>
{
    public async ValueTask<IronCondorTradePlanFunctionState> LoadStateAsync(
        UpdateIronCondorTradePlanCommand request, CancellationToken cancellationToken = default) =>
        (await LoadStateFromSnapshotAsync<IronCondorTradePlanFunctionState, IronCondorTradePlanUpdatedEvent>(
            request, cancellationToken).ConfigureAwait(false)).Prepare(request);

    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context,
        IronCondorTradePlanFunctionState state, UpdateIronCondorTradePlanCommand request,
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
