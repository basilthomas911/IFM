using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Actor;

/// <summary>Resolves the authoritative handoff repository and durable post-commit projector.</summary>
public sealed class StrategyExecutionHandoffCommandContext : CommandActorContext, IStrategyExecutionHandoffCommandContext
{
    public StrategyExecutionHandoffCommandContext(IActorSupervisor supervisor, ILogger<StrategyExecutionHandoffCommandActor> logger, TimeProvider clock)
        : base(supervisor, new(ActorType.Command, StrategyExecutionHandoffCommandActor.ActorName))
    { Logger = logger; TimeProvider = clock; }

    public ILogger<StrategyExecutionHandoffCommandActor> Logger { get; }
    public TimeProvider TimeProvider { get; }
    public IEventSourceActorDbContext DbEventSource => Container.Resolve<IEventSourceActorDbContext>();
    public IDurableReplayQueue DurableReplayQueue => Container.Resolve<IDurableReplayQueue>();
    public IActorService ActorService => Container.Resolve<IActorService>();
    public IEventProjector<StrategyExecutionHandoffCommandActor> EventProjector => Container.Resolve<IEventProjector<StrategyExecutionHandoffCommandActor>>();
    public IEventSourceActorStateRepository<StrategyExecutionHandoffCommandState> StateRepository => Container.Resolve<IEventSourceActorStateRepository<StrategyExecutionHandoffCommandState>>();
}
