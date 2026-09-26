using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>Requests durable publication of one immutable, session-aligned futures OHLCV bar.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PublishFuturesTradeSessionBarCommand : ICommand<FuturesTradeSessionBarEntityId>
{

    /// <summary>Creates an empty command for serialization.</summary>
    public PublishFuturesTradeSessionBarCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="bar">The Bar field.</param>
    [SerializationConstructor]
    public PublishFuturesTradeSessionBarCommand(Guid commandId, ActorSubject subject, bool postEvents, FuturesTradeSessionBarEntityId entityId, int errorCode, BoundedContextName routeTo, FuturesTradeSessionBarReadModel bar)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Bar = bar;
    }
    /// <summary>Gets the Command actor mailbox name.</summary>
    public const string Actor = "FuturesTradeSessionBarSignalCommand";
    /// <summary>Gets the command verb.</summary>
    public const string Verb = "Publish";
    /// <summary>Gets the stable command error code.</summary>
    public const int ErrorId = 26030;

    /// <inheritdoc />
    [Key(0)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(1)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(2)] public bool PostEvents { get; init; } = true;
    /// <inheritdoc />
    [Key(3)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [Key(5)] public BoundedContextName RouteTo { get; init; } =
        BoundedContextName.FuturesTradeSessionBarSignalBoundedContext;
    /// <summary>Gets the immutable completed bar to publish.</summary>
    [Key(6)] public FuturesTradeSessionBarReadModel Bar { get; init; } = new();
    /// <inheritdoc />
    [IgnoreMember] public string CommandName => nameof(PublishFuturesTradeSessionBarCommand);
    /// <inheritdoc />
    [IgnoreMember] public string StreamId => Subject.StreamId;
    /// <inheritdoc />
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <inheritdoc />
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    /// <inheritdoc />
    [IgnoreMember] public string OriginatedBy => string.Empty;
}
