using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Reference;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Queries;

/// <summary>Generates the current representative Regime Discovery decision reference without persistence.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetRegimeDiscoveryDecisionReferenceQuery : IQuery<RegimeDiscoveryDecisionReferenceDto[]>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetRegimeDiscoveryDecisionReferenceQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    [SerializationConstructor]
    public GetRegimeDiscoveryDecisionReferenceQuery(ActorSubject subject, IActorEntityId entityId)
    {
        Subject = subject;
        EntityId = entityId;
    }
    [IgnoreMember] public const string Actor = GetRegimeDiscoveryQuery.Actor;
    [IgnoreMember] public const string Verb = "GetDecisionReference";
    [IgnoreMember] public const int ErrorId = 23205;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
}
