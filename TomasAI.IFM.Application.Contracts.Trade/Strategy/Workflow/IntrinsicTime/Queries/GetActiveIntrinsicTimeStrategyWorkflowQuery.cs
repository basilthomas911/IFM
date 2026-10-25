using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;

/// <summary>Gets the active workflow for one stable workflow entity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetActiveIntrinsicTimeStrategyWorkflowQuery : IQuery<ActiveIntrinsicTimeStrategyWorkflowReadModel>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetActiveIntrinsicTimeStrategyWorkflowQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="workflowEntityId">The WorkflowEntityId field.</param>
    /// <param name="minimumWorkflowRevision">The MinimumWorkflowRevision field.</param>
    [SerializationConstructor]
    public GetActiveIntrinsicTimeStrategyWorkflowQuery(ActorSubject subject, IActorEntityId entityId, string workflowEntityId, long minimumWorkflowRevision)
    {
        Subject = subject;
        EntityId = entityId;
        WorkflowEntityId = workflowEntityId;
        MinimumWorkflowRevision = minimumWorkflowRevision;
    }
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetActive";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25002;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the formatted stable workflow entity identity.</summary>
    [Key(2)] public string WorkflowEntityId { get; init; } = string.Empty;
    /// <summary>Gets the minimum acceptable projection revision.</summary>
    [Key(3)] public long MinimumWorkflowRevision { get; init; }
}
