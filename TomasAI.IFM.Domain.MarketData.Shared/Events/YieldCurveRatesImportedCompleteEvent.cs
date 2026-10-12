using System;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>
/// Event published when yield curve rates have been imported successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record YieldCurveRatesImportedCompleteEvent : ICompleteEvent<YieldCurveRateEntityId>
{
    [Key(13)] public TomasAI.IFM.Domain.MarketData.Shared.DownloadLog.MarketDataDownloadOutcome? DownloadOutcome { get; init; }

    [IgnoreMember] public const string Actor = "YieldCurveRateEvent";
    [IgnoreMember] public const string Verb = "ImportedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public YieldCurveRateEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public DateTime ImportDate { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
    [Key(9)] public YieldCurveRateReadModel[] YieldCurveRates { get; init; }
    [Key(10)] public DateTime ImportedOn { get; init; }
    [Key(11)] public string ImportedBy { get; init; }
    [Key(12)] public ImportDuplicatePolicy DuplicatePolicy { get; init; } = ImportDuplicatePolicy.Overwrite;

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public YieldCurveRatesImportedCompleteEvent() { }

    [SerializationConstructor]
    public YieldCurveRatesImportedCompleteEvent(
        ActorSubject subject,
        YieldCurveRateEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateTime importDate,
        YieldCurveRateReadModel[] yieldCurveRates,
        DateTime importedOn,
        string importedBy,
        ImportDuplicatePolicy duplicatePolicy,
        TomasAI.IFM.Domain.MarketData.Shared.DownloadLog.MarketDataDownloadOutcome? downloadOutcome = null)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        ImportDate = importDate;
        YieldCurveRates = yieldCurveRates ?? [];
        ImportedOn = importedOn;
        ImportedBy = importedBy ?? string.Empty;
        DuplicatePolicy = duplicatePolicy;
        DownloadOutcome = downloadOutcome;
    }
}
