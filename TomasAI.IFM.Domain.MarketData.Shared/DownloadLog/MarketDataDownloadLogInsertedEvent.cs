using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

/// <summary>Reports that a market-data download outcome was recorded.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketDataDownloadLogInsertedEvent : IEvent<DownloadLogId>, IRequireDurableProjection
{
    [IgnoreMember, Newtonsoft.Json.JsonIgnore]
    public DurableProjectionRequirement RequiredProjection => new("DownloadLogCommandActor", "DownloadLogEventProjector",
        TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.PublishProcessingEvent);
    public const string Actor = "DownloadLogEvent";
    public const string Verb = "Inserted";
    [Key(0)] public ActorSubject Subject { get; init; } = default!;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public DownloadLogId EntityId { get; init; } = default!;
    [Key(3)] public long EventId { get; init; } = default;
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public string AggregateId { get; init; } = "";
    [Key(6)] public string EventSource { get; init; } = "";
    [Key(7)] public DateTime ReceivedOn { get; init; } = default;
    [Key(8)] public MarketDataDownloadOutcome Outcome { get; init; } = default!;
    [Key(9)] public string PayloadSha256 { get; init; } = "";
    [IgnoreMember] public string UserName => "MarketDataImport";
    [IgnoreMember] public string EventName => nameof(MarketDataDownloadLogInsertedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
    /// <summary>Creates the completed lifecycle notification from this committed event.</summary>
    /// <typeparam name="TTerminal">The requested completed-event contract.</typeparam>
    /// <typeparam name="TId">The entity identity contract.</typeparam>
    /// <returns>The corresponding completed event.</returns>
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
    /// <summary>Creates the failure lifecycle notification from this committed event.</summary>
    /// <typeparam name="TTerminal">The requested failure-event contract.</typeparam>
    /// <typeparam name="TId">The entity identity contract.</typeparam>
    /// <param name="ex">The processing failure to report.</param>
    /// <returns>The corresponding failure event.</returns>
    public IErrorEvent<TId> ToFailEvent<TTerminal, TId>(Exception ex) where TTerminal : IErrorEvent<TId> where TId : IActorEntityId
        
    {
        ArgumentNullException.ThrowIfNull(ex);
        return (IErrorEvent<TId>)(object)new MarketDataDownloadLogInsertedFailEvent
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
            ErrorDate = DateTime.UtcNow,
            ErrorMessage = ex.Message,
        };
    }
}
