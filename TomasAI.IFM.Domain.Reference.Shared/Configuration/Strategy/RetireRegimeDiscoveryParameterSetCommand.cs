using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;

/// <summary>Retires one published Regime Discovery parameter-set version.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record RetireRegimeDiscoveryParameterSetCommand
: ICommand<RegimeDiscoveryParameterSetEntityId>
{

    /// <summary>Creates an empty command for serialization.</summary>
    public RetireRegimeDiscoveryParameterSetCommand() { }

    /// <summary>Rehydrates the published command fields in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="retiredAtUtc">The RetiredAtUtc field.</param>
    [SerializationConstructor]
    public RetireRegimeDiscoveryParameterSetCommand(Guid commandId, ActorSubject subject, bool postEvents, RegimeDiscoveryParameterSetEntityId entityId, int errorCode, BoundedContextName routeTo, DateTime retiredAtUtc)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        RetiredAtUtc = retiredAtUtc;
    }
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
