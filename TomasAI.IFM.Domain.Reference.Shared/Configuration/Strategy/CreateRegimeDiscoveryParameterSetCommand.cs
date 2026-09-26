using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;

/// <summary>Creates an immutable Draft Regime Discovery parameter-set version.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record CreateRegimeDiscoveryParameterSetCommand
: ICommand<RegimeDiscoveryParameterSetEntityId>
{

    /// <summary>Creates an empty command for serialization.</summary>
    public CreateRegimeDiscoveryParameterSetCommand() { }

    /// <summary>Rehydrates the published command fields in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="parameterSet">The ParameterSet field.</param>
    /// <param name="description">The Description field.</param>
    /// <param name="createdBy">The CreatedBy field.</param>
    [SerializationConstructor]
    public CreateRegimeDiscoveryParameterSetCommand(Guid commandId, ActorSubject subject, bool postEvents, RegimeDiscoveryParameterSetEntityId entityId, int errorCode, BoundedContextName routeTo, RegimeDiscoveryParameterSet parameterSet, string description, string createdBy)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ParameterSet = parameterSet;
        Description = description;
        CreatedBy = createdBy;
    }
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
