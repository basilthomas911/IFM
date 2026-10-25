using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;

/// <summary>Gets one exact immutable Regime Discovery parameter-set version.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetRegimeDiscoveryParameterSetQuery : IQuery<RegimeDiscoveryParameterSet>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetRegimeDiscoveryParameterSetQuery() { }

    /// <summary>Rehydrates the published query fields in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="parameterSetId">The ParameterSetId field.</param>
    /// <param name="version">The Version field.</param>
    [SerializationConstructor]
    public GetRegimeDiscoveryParameterSetQuery(ActorSubject subject, IActorEntityId entityId, Guid parameterSetId, int version)
    {
        Subject = subject;
        EntityId = entityId;
        ParameterSetId = parameterSetId;
        Version = version;
    }
    /// <summary>Gets the configuration Query actor name.</summary>
    [IgnoreMember] public const string Actor = "RegimeDiscoveryConfigurationQuery";
    /// <summary>Gets the exact-version query verb.</summary>
    [IgnoreMember] public const string Verb = "Get";
    /// <summary>Gets the stable error code.</summary>
    [IgnoreMember] public const int ErrorId = 33101;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <summary>Gets the requested parameter identity.</summary>
    [Key(2)] public Guid ParameterSetId { get; init; }
    /// <summary>Gets the requested version.</summary>
    [Key(3)] public int Version { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
}
