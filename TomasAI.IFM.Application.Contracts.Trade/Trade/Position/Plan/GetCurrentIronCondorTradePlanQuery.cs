using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>Reads the current material Iron Condor Trade Plan.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetCurrentIronCondorTradePlanQuery : IQuery<StrategyTradePlanSnapshot?>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetCurrentIronCondorTradePlanQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="planId">The PlanId field.</param>
    [SerializationConstructor]
    public GetCurrentIronCondorTradePlanQuery(ActorSubject subject, IActorEntityId entityId, IronCondorTradePlanId planId)
    {
        Subject = subject;
        EntityId = entityId;
        PlanId = planId;
    }
    [IgnoreMember] public const string Actor = "IronCondorTradePlanQuery";
    [IgnoreMember] public const string Verb = "GetCurrentIronCondorTradePlan";
    [IgnoreMember] public const int ErrorId = 27121;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public IronCondorTradePlanId PlanId { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}
