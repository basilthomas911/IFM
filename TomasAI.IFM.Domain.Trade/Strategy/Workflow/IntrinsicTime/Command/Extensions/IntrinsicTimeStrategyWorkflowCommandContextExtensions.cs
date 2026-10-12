using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;

/// <summary>Exposes workflow Command services as readonly properties on its closed-generic context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# 14 extension properties use the context receiver and cannot be static receiver-independent properties.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 creates the nested extension receiver type; it is not a separately exposed handwritten nested API.")]
public static class IntrinsicTimeStrategyWorkflowCommandContextExtensions
{
    extension(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context)
    {
        /// <summary>Gets EventSourceDb.</summary>
        public IEventSourceActorDbContext DbEventSource => Typed(context).DbEventSource;
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory => Typed(context).DbFactory;
        /// <summary>Gets the conventional projector.</summary>
        public IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> EventProjector => Typed(context).EventProjector;
        /// <summary>Gets the authoritative workflow state repository.</summary>
        public IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> StateRepository => Typed(context).StateRepository;
        /// <summary>Gets the projector queue dependency.</summary>
        public IDurableReplayQueue DurableReplayQueue => Typed(context).DurableReplayQueue;
        /// <summary>Gets the projector blackboard.</summary>
        public IBlackboardService BlackboardService => Typed(context).BlackboardService;
        /// <summary>Gets the event-source state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => Typed(context).StateFactory;
        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => Typed(context).ActorService;
        /// <summary>Gets the workflow clock.</summary>
        public TimeProvider TimeProvider => Typed(context).TimeProvider;
        /// <summary>Gets the fixed workflow execution duration.</summary>
        public RegimeDiscoveryExecutionOptions ExecutionOptions => Typed(context).ExecutionOptions;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<IntrinsicTimeStrategyWorkflowCommandActor> Logger => Typed(context).Logger;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3398", Justification = "This ordinary helper validates the typed receiver used by every C# 14 extension property.")]
    static IIntrinsicTimeStrategyWorkflowCommandContext Typed(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context)
        => context as IIntrinsicTimeStrategyWorkflowCommandContext ?? throw new ArgumentException("The workflow command context must expose its typed services.", nameof(context));
}
