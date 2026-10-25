using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Reports terminal EMA projection failure.</summary>
[MessagePackObject]
public sealed record FuturesEmaSignalGeneratedFailEvent : IErrorEvent<FuturesTradeSessionBarEntityId>
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
    [IgnoreMember] public string EventName => nameof(FuturesEmaSignalGeneratedFailEvent);
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    internal static FuturesEmaSignalGeneratedFailEvent Create(FuturesEmaSignalGeneratedEvent source, Exception exception) => new()
    {
        Subject = new(ActorType.Event, FuturesEmaSignalGeneratedEvent.Actor, Verb, source.EntityId.Format()),
        EntityId = source.EntityId,
        Id = source.Id,
        ErrorDate = DateTime.UtcNow,
        EventId = source.EventId,
        CommandId = source.CommandId,
        EventSource = source.EventSource,
        ErrorMessage = exception.Message,
        ErrorCode = FuturesEmaSignalGeneratedEvent.ErrorCode,
        ErrorType = ErrorType.Command,
        ErrorData = exception.ToString(),
        ReceivedOn = source.ReceivedOn,
        AggregateId = source.AggregateId
    };
}
