using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
/// <summary>Defines readonly services owned by the Intrinsic Time Strategy Workflow Command actor.</summary>
public interface IIntrinsicTimeStrategyWorkflowCommandContext : ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>
{
    Domain.Portfolio.Shared.ServiceApi.IPortfolioQueryApi PortfolioQueries => throw new InvalidOperationException("Portfolio queries are not configured.");

    Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi FinancialApi => throw new InvalidOperationException("Portfolio financial API is not configured.");

    /// <summary>Gets the EventSourceDb context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }

    /// <summary>Gets the application database-context factory.</summary>
    IDbContextFactory DbFactory { get; }

    /// <summary>Gets the conventional workflow projector.</summary>
    IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> EventProjector { get; }

    /// <summary>Gets the authoritative event-source repository.</summary>
    IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> StateRepository { get; }

    /// <summary>Gets the non-durable queue dependency required by the projector base.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }

    /// <summary>Gets the blackboard service required by the projector base.</summary>
    IBlackboardService BlackboardService { get; }

    /// <summary>Gets the event-source state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }

    /// <summary>Gets the actor service used by the state repository.</summary>
    IActorService ActorService { get; }

    /// <summary>Gets the workflow clock.</summary>
    TimeProvider TimeProvider { get; }

    /// <summary>Gets the validated fixed workflow execution duration.</summary>
    RegimeDiscoveryExecutionOptions ExecutionOptions { get; }

    /// <summary>Gets the command actor logger.</summary>
    ILogger<IntrinsicTimeStrategyWorkflowCommandActor> Logger { get; }
}
