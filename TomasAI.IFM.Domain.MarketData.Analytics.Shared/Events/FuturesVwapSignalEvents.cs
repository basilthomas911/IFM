using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Records one replayable VWAP session-state transition.</summary>
[MessagePackObject]
public sealed record FuturesVwapSignalUpdatedEvent : IEvent<FuturesVwapSignalEntityId>
{
    public const string Actor = "FuturesVwapSignalEvent";
    public const string Verb = "SignalUpdated";
    public const int ErrorCode = 26410;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesVwapSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesVwapCheckpoint Checkpoint { get; init; } = new();
    [Key(9)] public FuturesVwapSignalReadModel Signal { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesVwapSignalUpdatedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId> where TEntityId : IActorEntityId =>
        (ICompleteEvent<TEntityId>)(object)new FuturesVwapSignalUpdatedCompleteEvent
        {
            Subject = new(ActorType.Event, Actor, FuturesVwapSignalUpdatedCompleteEvent.Verb, EntityId.Format()),
            EntityId = EntityId, Id = Id, EventId = EventId, CommandId = CommandId,
            AggregateId = AggregateId, EventSource = EventSource, ReceivedOn = ReceivedOn,
            Checkpoint = Checkpoint, Signal = Signal
        };

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception exception)
        where TFail : IErrorEvent<TEntityId> where TEntityId : IActorEntityId =>
        (IErrorEvent<TEntityId>)(object)FuturesVwapSignalUpdatedFailEvent.Create(this, exception);
}

/// <summary>Reports successful projection of a futures-session VWAP transition.</summary>
[MessagePackObject]
public sealed record FuturesVwapSignalUpdatedCompleteEvent : ICompleteEvent<FuturesVwapSignalEntityId>
{
    public const string Verb = "SignalUpdatedComplete";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesVwapSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesVwapCheckpoint Checkpoint { get; init; } = new();
    [Key(9)] public FuturesVwapSignalReadModel Signal { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesVwapSignalUpdatedCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

/// <summary>Reports terminal failure of a VWAP read-model projection.</summary>
[MessagePackObject]
public sealed record FuturesVwapSignalUpdatedFailEvent : IErrorEvent<FuturesVwapSignalEntityId>
{
    public const string Verb = "SignalUpdatedFail";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesVwapSignalEntityId EntityId { get; init; }
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
    [IgnoreMember] public string EventName => nameof(FuturesVwapSignalUpdatedFailEvent);
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    internal static FuturesVwapSignalUpdatedFailEvent Create(
        FuturesVwapSignalUpdatedEvent source, Exception exception) => new()
        {
            Subject = new(ActorType.Event, FuturesVwapSignalUpdatedEvent.Actor, Verb, source.EntityId.Format()),
            EntityId = source.EntityId, Id = source.Id, ErrorDate = DateTime.UtcNow,
            EventId = source.EventId, CommandId = source.CommandId, EventSource = source.EventSource,
            ErrorMessage = exception.Message, ErrorCode = FuturesVwapSignalUpdatedEvent.ErrorCode,
            ErrorType = ErrorType.Command, ErrorData = exception.ToString(), ReceivedOn = source.ReceivedOn,
            AggregateId = source.AggregateId
        };
}
