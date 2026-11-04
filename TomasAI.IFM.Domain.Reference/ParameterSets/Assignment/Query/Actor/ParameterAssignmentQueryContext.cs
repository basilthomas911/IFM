using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.State;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Query.Actor;

/// <summary>Provides the closed-generic strategy-configuration Query context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class ParameterAssignmentQueryContext
    : QueryActorContext,
      IParameterAssignmentQueryContext
{
    readonly Lazy<IParameterAccessPolicy> accessPolicy;
    public IParameterAccessPolicy AccessPolicy => accessPolicy.Value;

    readonly Lazy<IEventSourceActorStateRepository<ParameterAssignmentCommandState>> assignments;

    /// <summary>Initializes the Query context.</summary>
    public ParameterAssignmentQueryContext(
        IActorSupervisor supervisor,
        ILogger<ParameterAssignmentQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, ParameterAssignmentQueryActor.ActorName))
    {
        Logger = IsArgumentNull.Set(logger);
        accessPolicy = new(() => IsArgumentNull.Set(Container.Resolve<IParameterAccessPolicy>())!);
        assignments = new(() => IsArgumentNull.Set(Container.Resolve<IEventSourceActorStateRepository<ParameterAssignmentCommandState>>())!);
    }

    /// <inheritdoc />
    public IEventSourceActorStateRepository<ParameterAssignmentCommandState> Assignments => assignments.Value;
    /// <inheritdoc />
    public ILogger<ParameterAssignmentQueryActor> Logger { get; }
}
