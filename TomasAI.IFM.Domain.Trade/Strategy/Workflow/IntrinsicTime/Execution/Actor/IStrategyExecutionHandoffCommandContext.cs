using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Actor;

/// <summary>Closed actor services; command computations remain separate from external dispatch.</summary>
public interface IStrategyExecutionHandoffCommandContext : ICommandActorContext<StrategyExecutionHandoffCommandActor>
{
    IEventSourceActorDbContext DbEventSource { get; }
    IDurableReplayQueue DurableReplayQueue { get; }
    IActorService ActorService { get; }
    TimeProvider TimeProvider { get; }
    IEventProjector<StrategyExecutionHandoffCommandActor> EventProjector { get; }
    IEventSourceActorStateRepository<StrategyExecutionHandoffCommandState> StateRepository { get; }
    ILogger<StrategyExecutionHandoffCommandActor> Logger { get; }
}
