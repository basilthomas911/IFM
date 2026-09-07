using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

[MessagePackObject]
public sealed record MarketDataDownloadLogInsertedEvent : IEvent<DownloadLogId>, IRequireDurableProjection
{
    [IgnoreMember, Newtonsoft.Json.JsonIgnore]
    public DurableProjectionRequirement RequiredProjection => new("DownloadLogCommandActor", "DownloadLogEventProjector",
        TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.PublishProcessingEvent);
    public const string Actor = "DownloadLogEvent";
    public const string Verb = "Inserted";
    [Key(0)] public ActorSubject Subject { get; init; } = default!;
    [Key(1)] public Guid Id { get; init; } = default;
    [Key(2)] public DownloadLogId EntityId { get; init; } = default!;
    [Key(3)] public long EventId { get; init; } = default;
    [Key(4)] public Guid CommandId { get; init; } = default;
    [Key(5)] public string AggregateId { get; init; } = "";
    [Key(6)] public string EventSource { get; init; } = "";
    [Key(7)] public DateTime ReceivedOn { get; init; } = default;
    [Key(8)] public MarketDataDownloadOutcome Outcome { get; init; } = default!;
    [Key(9)] public string PayloadSha256 { get; init; } = "";
    [IgnoreMember] public string UserName => "MarketDataImport";
    [IgnoreMember] public string EventName => nameof(MarketDataDownloadLogInsertedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
    public ICompleteEvent<TId> ToCompleteEvent<TTerminal, TId>() where TTerminal : ICompleteEvent<TId> where TId : IActorEntityId
        => (ICompleteEvent<TId>)(object)new MarketDataDownloadLogInsertedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, Actor, "InsertedComplete", EntityId.Format()),
            Id = this.Id,
            EntityId = this.EntityId,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            Outcome = this.Outcome,
            PayloadSha256 = this.PayloadSha256,
        };
    public IErrorEvent<TId> ToFailEvent<TTerminal, TId>(Exception ex) where TTerminal : IErrorEvent<TId> where TId : IActorEntityId
        => (IErrorEvent<TId>)(object)new MarketDataDownloadLogInsertedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, Actor, "InsertedFail", EntityId.Format()),
            Id = this.Id,
            EntityId = this.EntityId,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            Outcome = this.Outcome,
            PayloadSha256 = this.PayloadSha256,
            ErrorDate = DateTime.UtcNow, ErrorMessage = ex.Message,
        };
}

[MessagePackObject]
public sealed record MarketDataDownloadLogInsertedCompleteEvent : ICompleteEvent<DownloadLogId>
{
    public const string Actor = "DownloadLogEvent";
    public const string Verb = "InsertedComplete";
    [Key(0)] public ActorSubject Subject { get; init; } = default!;
    [Key(1)] public Guid Id { get; init; } = default;
    [Key(2)] public DownloadLogId EntityId { get; init; } = default!;
    [Key(3)] public long EventId { get; init; } = default;
    [Key(4)] public Guid CommandId { get; init; } = default;
    [Key(5)] public string AggregateId { get; init; } = "";
    [Key(6)] public string EventSource { get; init; } = "";
    [Key(7)] public DateTime ReceivedOn { get; init; } = default;
    [Key(8)] public MarketDataDownloadOutcome Outcome { get; init; } = default!;
    [Key(9)] public string PayloadSha256 { get; init; } = "";
    [IgnoreMember] public string UserName => "MarketDataImport";
    [IgnoreMember] public string EventName => nameof(MarketDataDownloadLogInsertedCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

[MessagePackObject]
public sealed record MarketDataDownloadLogInsertedFailEvent : IErrorEvent<DownloadLogId>
{
    public const string Actor = "DownloadLogEvent";
    public const string Verb = "InsertedFail";
    [Key(0)] public ActorSubject Subject { get; init; } = default!;
    [Key(1)] public Guid Id { get; init; } = default;
    [Key(2)] public DownloadLogId EntityId { get; init; } = default!;
    [Key(3)] public long EventId { get; init; } = default;
    [Key(4)] public Guid CommandId { get; init; } = default;
    [Key(5)] public string AggregateId { get; init; } = "";
    [Key(6)] public string EventSource { get; init; } = "";
    [Key(7)] public DateTime ReceivedOn { get; init; } = default;
    [Key(8)] public MarketDataDownloadOutcome Outcome { get; init; } = default!;
    [Key(9)] public string PayloadSha256 { get; init; } = "";
    [Key(10)] public DateTime ErrorDate { get; init; } = default;
    [Key(11)] public int ErrorCode { get; init; } = 6050;
    [Key(12)] public string ErrorMessage { get; init; } = "";
    [Key(13)] public ErrorType ErrorType { get; init; } = ErrorType.Command;
    [Key(14)] public string ErrorData { get; init; } = "";
    [Key(15)] public string CommandName { get; init; } = "";
    [Key(16)] public string CommandData { get; init; } = "";
    [IgnoreMember] public string UserName => "MarketDataImport";
    [IgnoreMember] public string EventName => nameof(MarketDataDownloadLogInsertedFailEvent);
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
