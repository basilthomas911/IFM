using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>
/// Event published when economic calendars have been imported successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record EconomicCalendarsImportedCompleteEvent : ICompleteEvent<EconomicCalendarId>
{
    [Key(14)] public TomasAI.IFM.Domain.MarketData.Shared.DownloadLog.MarketDataDownloadOutcome? DownloadOutcome { get; init; }

    [IgnoreMember] public const string Actor = "EconomicCalendarEvent";
    [IgnoreMember] public const string Verb = "ImportedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public EconomicCalendarId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
    [Key(8)] public EconomicCalendarReadModel[] EconomicCalendars { get; init; }
    [Key(9)] public DateTime ImportedOn { get; init; }
    [Key(10)] public string ImportedBy { get; init; }
    [Key(11)] public ImportDuplicatePolicy DuplicatePolicy { get; init; } = ImportDuplicatePolicy.Overwrite;
    [Key(12)] public DateTime ImportedDate { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
    [Key(13)] public string[] CountryCodes { get; init; } = [];

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public EconomicCalendarsImportedCompleteEvent() { }

    [SerializationConstructor]
    public EconomicCalendarsImportedCompleteEvent(
        ActorSubject subject,
        EconomicCalendarId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        EconomicCalendarReadModel[] economicCalendars,
        DateTime importedOn,
        string importedBy,
        ImportDuplicatePolicy duplicatePolicy,
        DateTime importedDate,
        string[] countryCodes,
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
        EconomicCalendars = economicCalendars ?? [];
        ImportedOn = importedOn;
        ImportedBy = importedBy ?? string.Empty;
        DuplicatePolicy = duplicatePolicy;
        ImportedDate = importedDate;
        CountryCodes = countryCodes ?? [];
        DownloadOutcome = downloadOutcome;
    }
}
