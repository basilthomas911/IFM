using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;

public sealed class MarketConditionFunctionStateRepository(
    IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource,
    IActorService actorService, ILogger<MarketConditionFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceFunctionStateRepository<MarketConditionFunctionState, ExecuteMarketConditionPipelineCommand>
{
    public async ValueTask<MarketConditionFunctionState> LoadStateAsync(
        ExecuteMarketConditionPipelineCommand request, CancellationToken token = default)
        => await LoadStateAsync<MarketConditionFunctionState>(request, token).ConfigureAwait(false);
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context,
        MarketConditionFunctionState state, ExecuteMarketConditionPipelineCommand request,
        CancellationToken token = default)
        => await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, token).ConfigureAwait(false);
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => ValueTask.CompletedTask;
}
