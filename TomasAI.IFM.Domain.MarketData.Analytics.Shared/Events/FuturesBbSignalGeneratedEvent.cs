using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Records one event-sourced Bollinger state transition.</summary>
[MessagePackObject]
public sealed record FuturesBbSignalGeneratedEvent : IEvent<FuturesTradeSessionBarEntityId>
{
    public const string Actor = "FuturesBbSignalEvent";
    public const string Verb = "SignalGenerated";
    public const int ErrorCode = 26111;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the generated Bollinger signal.</summary>
    [Key(8)] public FuturesBbSignalReadModel Signal { get; init; } = new();
    /// <summary>Gets the replayable Bollinger checkpoint.</summary>
    [Key(9)] public FuturesBbAccumulatorCheckpoint Checkpoint { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesBbSignalGeneratedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <inheritdoc />
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId> where TEntityId : IActorEntityId =>
        (ICompleteEvent<TEntityId>)(object)new FuturesBbSignalGeneratedCompleteEvent
        {
            Subject = new(ActorType.Event, Actor, FuturesBbSignalGeneratedCompleteEvent.Verb, EntityId.Format()),
            EntityId = EntityId, Id = Id, EventId = EventId, CommandId = CommandId,
            AggregateId = AggregateId, EventSource = EventSource, ReceivedOn = ReceivedOn,
            Signal = Signal, Checkpoint = Checkpoint
        };

    /// <inheritdoc />
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception exception)
        where TFail : IErrorEvent<TEntityId> where TEntityId : IActorEntityId =>
        (IErrorEvent<TEntityId>)(object)FuturesBbSignalGeneratedFailEvent.Create(this, exception);
}

/// <summary>Reports successful Bollinger projection.</summary>
[MessagePackObject]
public sealed record FuturesBbSignalGeneratedCompleteEvent : ICompleteEvent<FuturesTradeSessionBarEntityId>
{
    public const string Verb = "SignalGeneratedComplete";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesBbSignalReadModel Signal { get; init; } = new();
    [Key(9)] public FuturesBbAccumulatorCheckpoint Checkpoint { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesBbSignalGeneratedCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

/// <summary>Reports terminal Bollinger projection failure.</summary>
[MessagePackObject]
public sealed record FuturesBbSignalGeneratedFailEvent : IErrorEvent<FuturesTradeSessionBarEntityId>
{
    public const string Verb = "SignalGeneratedFail";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public DateTime ErrorDate { get; init; }
    [Key(4)] public long EventId { get; init; }
    [Key(5)] public Guid CommandId { get; init; }
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(8)] public int ErrorCode { get; init; }
    [Key(9)] public ErrorType ErrorType { get; init; }
    [Key(10)] public string ErrorData { get; init; } = string.Empty;
    [Key(11)] public DateTime ReceivedOn { get; init; }
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    [Key(13)] public string CommandName { get; init; } = string.Empty;
    [Key(14)] public string CommandData { get; init; } = string.Empty;
    [Key(15)] public string RouteTo { get; init; } = string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesBbSignalGeneratedFailEvent);
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    internal static FuturesBbSignalGeneratedFailEvent Create(FuturesBbSignalGeneratedEvent source, Exception exception) => new()
    {
        Subject = new(ActorType.Event, FuturesBbSignalGeneratedEvent.Actor, Verb, source.EntityId.Format()),
        EntityId = source.EntityId, Id = source.Id, ErrorDate = DateTime.UtcNow,
        EventId = source.EventId, CommandId = source.CommandId, EventSource = source.EventSource,
        ErrorMessage = exception.Message, ErrorCode = FuturesBbSignalGeneratedEvent.ErrorCode,
        ErrorType = ErrorType.Command, ErrorData = exception.ToString(), ReceivedOn = source.ReceivedOn,
        AggregateId = source.AggregateId
    };
}
