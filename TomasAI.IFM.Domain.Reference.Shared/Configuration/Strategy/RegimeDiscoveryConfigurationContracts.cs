using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;

/// <summary>Identifies one immutable Regime Discovery parameter-set version.</summary>
[MessagePackObject]
public readonly record struct RegimeDiscoveryParameterSetEntityId(
    [property: Key(0)] Guid ParameterSetId,
    [property: Key(1)] int Version) : IActorEntityId
{
    /// <summary>Formats the stable actor routing identity.</summary>
    public string Format() => $"{ParameterSetId:N}.{Version}";
    /// <inheritdoc />
    public override string ToString() => Format();
}

/// <summary>Creates an immutable Draft Regime Discovery parameter-set version.</summary>
[MessagePackObject]
public sealed record CreateRegimeDiscoveryParameterSetCommand
    : ICommand<RegimeDiscoveryParameterSetEntityId>
{
    /// <summary>Gets the configuration Command actor name.</summary>
    [IgnoreMember] public const string Actor = "RegimeDiscoveryConfigurationCommand";
    /// <summary>Gets the create verb.</summary>
    [IgnoreMember] public const string Verb = "Create";
    /// <summary>Gets the stable error code.</summary>
    [IgnoreMember] public const int ErrorId = 33001;
    /// <inheritdoc />
    [Key(0)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(1)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(2)] public bool PostEvents { get; init; } = true;
    /// <inheritdoc />
    [Key(3)] public RegimeDiscoveryParameterSetEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.StrategyConfigurationBoundedContext;
    /// <summary>Gets the immutable typed parameter set.</summary>
    [Key(6)] public RegimeDiscoveryParameterSet ParameterSet { get; init; } = new();
    /// <summary>Gets its descriptive purpose.</summary>
    [Key(7)] public string Description { get; init; } = string.Empty;
    /// <summary>Gets the author identity.</summary>
    [Key(8)] public string CreatedBy { get; init; } = string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string CommandName => nameof(CreateRegimeDiscoveryParameterSetCommand);
    /// <inheritdoc />
    [IgnoreMember] public string StreamId => Subject.StreamId;
    /// <inheritdoc />
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <inheritdoc />
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    /// <inheritdoc />
    [IgnoreMember] public string OriginatedBy => CreatedBy;
}

/// <summary>Publishes one immutable Regime Discovery parameter-set version.</summary>
[MessagePackObject]
public sealed record PublishRegimeDiscoveryParameterSetCommand
    : ICommand<RegimeDiscoveryParameterSetEntityId>
{
    /// <summary>Gets the shared Command actor name.</summary>
    [IgnoreMember] public const string Actor = CreateRegimeDiscoveryParameterSetCommand.Actor;
    /// <summary>Gets the publish verb.</summary>
    [IgnoreMember] public const string Verb = "Publish";
    /// <summary>Gets the stable error code.</summary>
    [IgnoreMember] public const int ErrorId = 33002;
    /// <inheritdoc />
    [Key(0)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(1)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(2)] public bool PostEvents { get; init; } = true;
    /// <inheritdoc />
    [Key(3)] public RegimeDiscoveryParameterSetEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.StrategyConfigurationBoundedContext;
    /// <summary>Gets the UTC effective timestamp.</summary>
    [Key(6)] public DateTime EffectiveFromUtc { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public string CommandName => nameof(PublishRegimeDiscoveryParameterSetCommand);
    /// <inheritdoc />
    [IgnoreMember] public string StreamId => Subject.StreamId;
    /// <inheritdoc />
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <inheritdoc />
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    /// <inheritdoc />
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
}

/// <summary>Retires one published Regime Discovery parameter-set version.</summary>
[MessagePackObject]
public sealed record RetireRegimeDiscoveryParameterSetCommand
    : ICommand<RegimeDiscoveryParameterSetEntityId>
{
    /// <summary>Gets the shared Command actor name.</summary>
    [IgnoreMember] public const string Actor = CreateRegimeDiscoveryParameterSetCommand.Actor;
    /// <summary>Gets the retire verb.</summary>
    [IgnoreMember] public const string Verb = "Retire";
    /// <summary>Gets the stable error code.</summary>
    [IgnoreMember] public const int ErrorId = 33003;
    /// <inheritdoc />
    [Key(0)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(1)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(2)] public bool PostEvents { get; init; } = true;
    /// <inheritdoc />
    [Key(3)] public RegimeDiscoveryParameterSetEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.StrategyConfigurationBoundedContext;
    /// <summary>Gets the UTC retirement timestamp.</summary>
    [Key(6)] public DateTime RetiredAtUtc { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public string CommandName => nameof(RetireRegimeDiscoveryParameterSetCommand);
    /// <inheritdoc />
    [IgnoreMember] public string StreamId => Subject.StreamId;
    /// <inheritdoc />
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <inheritdoc />
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    /// <inheritdoc />
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
}

/// <summary>Gets one exact immutable Regime Discovery parameter-set version.</summary>
[MessagePackObject]
public sealed record GetRegimeDiscoveryParameterSetQuery : IQuery<RegimeDiscoveryParameterSet>
{
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

/// <summary>Gets the effective published Regime Discovery parameter set at one UTC timestamp.</summary>
[MessagePackObject]
public sealed record ResolveRegimeDiscoveryParameterSetQuery : IQuery<RegimeDiscoveryParameterSet>
{
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
