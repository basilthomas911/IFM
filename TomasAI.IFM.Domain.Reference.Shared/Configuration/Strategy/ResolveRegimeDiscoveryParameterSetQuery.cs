using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;

/// <summary>Gets the effective published Regime Discovery parameter set at one UTC timestamp.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ResolveRegimeDiscoveryParameterSetQuery : IQuery<RegimeDiscoveryParameterSet>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public ResolveRegimeDiscoveryParameterSetQuery() { }

    /// <summary>Rehydrates the published query fields in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="effectiveAtUtc">The EffectiveAtUtc field.</param>
    /// <param name="targetHorizon">The TargetHorizon field.</param>
    [SerializationConstructor]
    public ResolveRegimeDiscoveryParameterSetQuery(ActorSubject subject, IActorEntityId entityId, DateTime effectiveAtUtc, TimeFrameType targetHorizon)
    {
        Subject = subject;
        EntityId = entityId;
        EffectiveAtUtc = effectiveAtUtc;
        TargetHorizon = targetHorizon;
    }
    /// <summary>Gets the shared Query actor name.</summary>
    [IgnoreMember] public const string Actor = GetRegimeDiscoveryParameterSetQuery.Actor;
    /// <summary>Gets the effective-resolution verb.</summary>
    [IgnoreMember] public const string Verb = "ResolveEffective";
    /// <summary>Gets the stable error code.</summary>
    [IgnoreMember] public const int ErrorId = 33102;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <summary>Gets the requested effective UTC timestamp.</summary>
    [Key(2)] public DateTime EffectiveAtUtc { get; init; }
    /// <summary>Gets the workflow target horizon whose effective version is requested.</summary>
    [Key(3)] public TimeFrameType TargetHorizon { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
}
