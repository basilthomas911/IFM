using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;

/// <summary>Requests durable publication of one immutable, session-aligned futures OHLCV bar.</summary>
[MessagePackObject]
public sealed record PublishFuturesTradeSessionBarCommand : ICommand<FuturesTradeSessionBarEntityId>
{
    /// <summary>Gets the Command actor mailbox name.</summary>
    public const string Actor = "FuturesTradeSessionBarPublisherCommand";
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
        BoundedContextName.FuturesTradeSessionBarPublisherBoundedContext;
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

/// <summary>Records one completed futures trade-session bar in the ACID event stream.</summary>
[MessagePackObject]
public sealed record FuturesTradeSessionBarPublishedEvent : IEvent<FuturesTradeSessionBarEntityId>
{
    /// <summary>Gets the Event actor mailbox name.</summary>
    public const string Actor = "FuturesTradeSessionBarPublisherEvent";
    /// <summary>Gets the source event verb.</summary>
    public const string Verb = "Published";
    /// <summary>Gets the stable projection error code.</summary>
    public const int ErrorCode = PublishFuturesTradeSessionBarCommand.ErrorId;

    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(2)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(3)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(4)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the immutable completed bar.</summary>
    [Key(8)] public FuturesTradeSessionBarReadModel Bar { get; init; } = new();
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesTradeSessionBarPublishedEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Creates the terminal event emitted after successful ScyllaDB projection.</summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesTradeSessionBarEntityId))
            throw new InvalidOperationException($"Unsupported entity identity {typeof(TEntityId).Name}.");
        ICompleteEvent<FuturesTradeSessionBarEntityId> complete =
            new FuturesTradeSessionBarPublishedCompleteEvent
            {
                Subject = new(ActorType.Event, Actor,
                    FuturesTradeSessionBarPublishedCompleteEvent.Verb, EntityId.Format()),
                Id = Id,
                EntityId = EntityId,
                EventId = EventId,
                CommandId = CommandId,
                AggregateId = AggregateId,
                EventSource = EventSource,
                ReceivedOn = ReceivedOn,
                Bar = Bar
            };
        return (ICompleteEvent<TEntityId>)complete;
    }

    /// <summary>Creates the terminal failure event emitted when ScyllaDB projection fails.</summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception exception)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (typeof(TEntityId) != typeof(FuturesTradeSessionBarEntityId))
            throw new InvalidOperationException($"Unsupported entity identity {typeof(TEntityId).Name}.");
        IErrorEvent<FuturesTradeSessionBarEntityId> failed =
            new FuturesTradeSessionBarPublishedFailEvent
            {
                Subject = new(ActorType.Event, Actor,
                    FuturesTradeSessionBarPublishedFailEvent.Verb, EntityId.Format()),
                Id = Id,
                EntityId = EntityId,
                EventId = EventId,
                CommandId = CommandId == Guid.Empty ? Guid.NewGuid() : CommandId,
                AggregateId = AggregateId,
                EventSource = EventSource,
                ReceivedOn = ReceivedOn,
                ErrorDate = DateTime.UtcNow,
                ErrorMessage = exception.Message,
                ErrorCode = ErrorCode,
                ErrorType = ErrorType.Command,
                ErrorData = exception.ToString(),
                CommandName = nameof(PublishFuturesTradeSessionBarCommand),
                CommandData = string.Empty,
                RouteTo = BoundedContextName.FuturesTradeSessionBarPublisherBoundedContext.ToString()
            };
        return (IErrorEvent<TEntityId>)failed;
    }
}

/// <summary>Reports successful persistence of a published futures trade-session bar.</summary>
[MessagePackObject]
public sealed record FuturesTradeSessionBarPublishedCompleteEvent
    : ICompleteEvent<FuturesTradeSessionBarEntityId>
{
    /// <summary>Gets the completion verb.</summary>
    public const string Verb = "PublishedComplete";
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(2)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(3)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(4)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the persisted completed bar.</summary>
    [Key(8)] public FuturesTradeSessionBarReadModel Bar { get; init; } = new();
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesTradeSessionBarPublishedCompleteEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

/// <summary>Reports failure to project a published futures trade-session bar.</summary>
[MessagePackObject]
public sealed record FuturesTradeSessionBarPublishedFailEvent
    : IErrorEvent<FuturesTradeSessionBarEntityId>
{
    /// <summary>Gets the failure verb.</summary>
    public const string Verb = "PublishedFail";
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(2)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(3)] public DateTime ErrorDate { get; init; }
    /// <inheritdoc />
    [Key(4)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(5)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public string ErrorMessage { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(8)] public int ErrorCode { get; init; }
    /// <inheritdoc />
    [Key(9)] public ErrorType ErrorType { get; init; }
    /// <inheritdoc />
    [Key(10)] public string ErrorData { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(11)] public DateTime ReceivedOn { get; init; }
    /// <inheritdoc />
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(13)] public string CommandName { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(14)] public string CommandData { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(15)] public string RouteTo { get; init; } = string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesTradeSessionBarPublishedFailEvent);
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
