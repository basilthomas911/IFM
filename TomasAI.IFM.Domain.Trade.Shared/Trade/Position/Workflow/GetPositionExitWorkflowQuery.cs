using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

/// <summary>Reads the latest exit workflow stage for a strategy position and value date.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetPositionExitWorkflowQuery : IQuery<ExitPositionWorkflowProjection?>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetPositionExitWorkflowQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="positionId">The PositionId field.</param>
    /// <param name="valueDate">The ValueDate field.</param>
    [SerializationConstructor]
    public GetPositionExitWorkflowQuery(ActorSubject subject, IActorEntityId entityId, StrategyPositionId positionId, DateOnly valueDate)
    {
        Subject = subject;
        EntityId = entityId;
        PositionId = positionId;
        ValueDate = valueDate;
    }
    [IgnoreMember] public const string Actor = "PositionExitWorkflowQuery";
    [IgnoreMember] public const string Verb = "GetPositionExitWorkflow";
    [IgnoreMember] public const int ErrorId = 27231;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public StrategyPositionId PositionId { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}
