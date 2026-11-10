using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.State;

/// <summary>Loads the newest complete intent/checkpoint and retains durable post-commit dispatch evidence.</summary>
public sealed class StrategyExecutionHandoffStateRepository(IEventSourceActorStateFactory factory, IEventSourceActorDbContext source,
    IActorService actors, IEventProjector<StrategyExecutionHandoffCommandActor> projector, ILogger<StrategyExecutionHandoffStateRepository> logger)
    : BaseEventSourceActorRepository(factory, source, actors, logger), IEventSourceActorStateRepository<StrategyExecutionHandoffCommandState>
{
    public ValueTask<StrategyExecutionHandoffCommandState> LoadStateAsync(ICommand command) => new(
        LoadStateFromLatestCheckpointAsync<StrategyExecutionHandoffCommandState, StrategyExecutionHandoffStateSnapshottedEvent>(command,
            StrategyExecutionHandoffCommandState.IsComplete));

    public ValueTask SaveStateAsync(ICommandActorContext context, StrategyExecutionHandoffCommandState state, ICommand command)
        => new(SaveStateAndDenormalizeEventsAsync(context, state, command));

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => projector.DomainEventsProjectionAsync(events);
}
