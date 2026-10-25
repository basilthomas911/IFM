using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>Records one completed futures trade-session bar in the ACID event stream.</summary>
[MessagePackObject]
public sealed record FuturesTradeSessionBarPublishedEvent : IEvent<FuturesTradeSessionBarEntityId>
{
    /// <summary>Gets the Event actor mailbox name.</summary>
    public const string Actor = "FuturesTradeSessionBarSignalEvent";
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
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(ex);
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
                ErrorMessage = ex.Message,
                ErrorCode = ErrorCode,
                ErrorType = ErrorType.Command,
                ErrorData = ex.ToString(),
                CommandName = nameof(PublishFuturesTradeSessionBarCommand),
                CommandData = string.Empty,
                RouteTo = BoundedContextName.FuturesTradeSessionBarSignalBoundedContext.ToString()
            };
        return (IErrorEvent<TEntityId>)failed;
    }
}
