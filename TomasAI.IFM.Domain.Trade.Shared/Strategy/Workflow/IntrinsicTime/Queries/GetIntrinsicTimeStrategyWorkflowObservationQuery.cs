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

/// <summary>Gets the read-only operational condition of one stable workflow entity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetIntrinsicTimeStrategyWorkflowObservationQuery
    : IQuery<IntrinsicTimeStrategyWorkflowObservationReadModel>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetIntrinsicTimeStrategyWorkflowObservationQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="workflowEntity">The WorkflowEntity field.</param>
    [SerializationConstructor]
    public GetIntrinsicTimeStrategyWorkflowObservationQuery(ActorSubject subject, IActorEntityId entityId, IntrinsicTimeStrategyWorkflowEntityId workflowEntity)
    {
        Subject = subject;
        EntityId = entityId;
        WorkflowEntity = workflowEntity;
    }
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    [IgnoreMember] public const string Verb = "GetObservation";
    [IgnoreMember] public const int ErrorId = 25010;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
    [Key(2)] public IntrinsicTimeStrategyWorkflowEntityId WorkflowEntity { get; init; } = new();
}
