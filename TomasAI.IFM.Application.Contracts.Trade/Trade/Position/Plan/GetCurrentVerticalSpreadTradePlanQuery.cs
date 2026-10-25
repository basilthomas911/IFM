using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>Reads the current material Vertical Spread Trade Plan.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetCurrentVerticalSpreadTradePlanQuery : IQuery<StrategyTradePlanSnapshot?>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetCurrentVerticalSpreadTradePlanQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="planId">The PlanId field.</param>
    [SerializationConstructor]
    public GetCurrentVerticalSpreadTradePlanQuery(ActorSubject subject, IActorEntityId entityId, VerticalSpreadTradePlanId planId)
    {
        Subject = subject;
        EntityId = entityId;
        PlanId = planId;
    }
    [IgnoreMember] public const string Actor = "VerticalSpreadTradePlanQuery";
    [IgnoreMember] public const string Verb = "GetCurrentVerticalSpreadTradePlan";
    [IgnoreMember] public const int ErrorId = 27123;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public VerticalSpreadTradePlanId PlanId { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}
