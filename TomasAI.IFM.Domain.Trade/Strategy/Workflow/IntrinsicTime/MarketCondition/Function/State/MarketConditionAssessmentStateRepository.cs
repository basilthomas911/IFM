using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;

public sealed class MarketConditionAssessmentStateRepository(
    IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource,
    IActorService actorService, ILogger<MarketConditionAssessmentStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceFunctionStateRepository<MarketConditionAssessmentState, ExecuteMarketConditionAssessmentCommand>
{
    public async ValueTask<MarketConditionAssessmentState> LoadStateAsync(
        ExecuteMarketConditionAssessmentCommand request, CancellationToken token = default)
        => await LoadStateAsync<MarketConditionAssessmentState>(request, token).ConfigureAwait(false);
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context,
        MarketConditionAssessmentState state, ExecuteMarketConditionAssessmentCommand request,
        CancellationToken token = default)
    {
        using var activity = MarketConditionTelemetry.Start("market-condition.completed-state-append");
        await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, token).ConfigureAwait(false);
    }
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => ValueTask.CompletedTask;
}
