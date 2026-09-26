using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Requests one durable, idempotent futures Analytics history data load.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record LoadFuturesAnalyticsHistoricalDataCommand
    : ICommand<FuturesAnalyticsHistoricalDataLoaderEntityId>
{

    /// <summary>Creates an empty command for serialization.</summary>
    public LoadFuturesAnalyticsHistoricalDataCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="parameters">The Parameters field.</param>
    [SerializationConstructor]
    public LoadFuturesAnalyticsHistoricalDataCommand(Guid commandId, ActorSubject subject, bool postEvents, FuturesAnalyticsHistoricalDataLoaderEntityId entityId, int errorCode, BoundedContextName routeTo, FuturesAnalyticsHistoricalDataLoaderParameters parameters)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Parameters = parameters;
    }
    /// <summary>Gets the command actor name.</summary>
    public const string Actor = "FuturesAnalyticsHistoricalDataLoaderCommand";
    /// <summary>Gets the command verb.</summary>
    public const string Verb = "Load";
    /// <summary>Gets the stable command error code.</summary>
    public const int ErrorId = 26020;

    /// <inheritdoc />
    [Key(0)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(1)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(2)] public bool PostEvents { get; init; } = true;
    /// <inheritdoc />
    [Key(3)] public FuturesAnalyticsHistoricalDataLoaderEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    /// <summary>Gets the immutable provider-neutral request parameters.</summary>
    [Key(6)] public FuturesAnalyticsHistoricalDataLoaderParameters Parameters { get; init; } = new();
    /// <inheritdoc />
    [IgnoreMember] public string CommandName => nameof(LoadFuturesAnalyticsHistoricalDataCommand);
    /// <inheritdoc />
    [IgnoreMember] public string StreamId => Subject.StreamId;
    /// <inheritdoc />
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <inheritdoc />
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    /// <inheritdoc />
    [IgnoreMember] public string OriginatedBy => Parameters.RequestedBy;
}
