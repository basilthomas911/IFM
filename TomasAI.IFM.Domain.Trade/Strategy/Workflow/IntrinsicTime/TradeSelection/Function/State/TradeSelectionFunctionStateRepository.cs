using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;

using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.State;

public sealed class TradeSelectionFunctionStateRepository(
    IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource,
    IActorService actorService, ILogger<TradeSelectionFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceFunctionStateRepository<TradeSelectionFunctionState, ExecuteTradeSelectionPipelineCommand>
{
    public async ValueTask<TradeSelectionFunctionState> LoadStateAsync(
        ExecuteTradeSelectionPipelineCommand request, CancellationToken token = default)
        => await LoadStateAsync<TradeSelectionFunctionState>(request, token).ConfigureAwait(false);
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context,
        TradeSelectionFunctionState state, ExecuteTradeSelectionPipelineCommand request,
        CancellationToken token = default)
    {
        await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, token).ConfigureAwait(false);
    }
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => ValueTask.CompletedTask;
}
