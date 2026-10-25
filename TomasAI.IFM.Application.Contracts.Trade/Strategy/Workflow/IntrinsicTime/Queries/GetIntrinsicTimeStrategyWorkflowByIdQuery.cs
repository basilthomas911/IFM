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

/// <summary>Gets one workflow execution by its immutable workflow identity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetIntrinsicTimeStrategyWorkflowByIdQuery : IQuery<IntrinsicTimeStrategyWorkflowReadModel>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetIntrinsicTimeStrategyWorkflowByIdQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="workflowId">The WorkflowId field.</param>
    /// <param name="minimumWorkflowRevision">The MinimumWorkflowRevision field.</param>
    [SerializationConstructor]
    public GetIntrinsicTimeStrategyWorkflowByIdQuery(ActorSubject subject, IActorEntityId entityId, StrategyWorkflowId workflowId, long minimumWorkflowRevision)
    {
        Subject = subject;
        EntityId = entityId;
        WorkflowId = workflowId;
        MinimumWorkflowRevision = minimumWorkflowRevision;
    }
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflowQuery";
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetById";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25001;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the requested workflow identity.</summary>
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the minimum acceptable projection revision.</summary>
    [Key(3)] public long MinimumWorkflowRevision { get; init; }
}
