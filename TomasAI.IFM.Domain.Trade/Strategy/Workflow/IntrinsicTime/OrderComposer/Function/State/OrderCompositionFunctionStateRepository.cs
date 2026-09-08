using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;

using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.State;

public sealed class OrderCompositionFunctionStateRepository(
    IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource,
    IActorService actorService, ILogger<OrderCompositionFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceFunctionStateRepository<OrderCompositionFunctionState, ExecuteOrderCompositionPipelineCommand>
{
    public async ValueTask<OrderCompositionFunctionState> LoadStateAsync(
        ExecuteOrderCompositionPipelineCommand request, CancellationToken token = default)
        => await LoadStateAsync<OrderCompositionFunctionState>(request, token).ConfigureAwait(false);
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context,
        OrderCompositionFunctionState state, ExecuteOrderCompositionPipelineCommand request,
        CancellationToken token = default)
    {
        await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, token).ConfigureAwait(false);
    }
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => ValueTask.CompletedTask;
}
