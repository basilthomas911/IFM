using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>
/// Event published when importing economic calendars fails.
/// Carries standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record EconomicCalendarsImportedFailEvent : IErrorEvent<EconomicCalendarId>
{
    [Key(19)] public TomasAI.IFM.Domain.MarketData.Shared.DownloadLog.MarketDataDownloadOutcome? DownloadOutcome { get; init; }

    [IgnoreMember] public const string Actor = "EconomicCalendarEvent";
    [IgnoreMember] public const string Verb = "ImportedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public EconomicCalendarId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public DateTime ErrorDate { get; init; }
    [Key(4)] public long EventId { get; init; }
    [Key(5)] public Guid CommandId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public string ErrorMessage { get; init; }
    [Key(8)] public int ErrorCode { get; init; }
    [Key(9)] public ErrorType ErrorType { get; init; }
    [Key(10)] public string ErrorData { get; init; }
    [Key(11)] public DateTime ReceivedOn { get; init; }
    [Key(12)] public string AggregateId { get; init; }
    [Key(13)] public string CommandName { get; init; }
    [Key(14)] public string CommandData { get; init; }
    [Key(15)] public string RouteTo { get; init; }
    [Key(16)] public DateTime ImportedDate { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
    [Key(17)] public string[] CountryCodes { get; init; } = [];
    [Key(18)] public ImportDuplicatePolicy DuplicatePolicy { get; init; } = ImportDuplicatePolicy.Overwrite;

    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    public EconomicCalendarsImportedFailEvent() { }

    [SerializationConstructor]
    public EconomicCalendarsImportedFailEvent(
        ActorSubject subject,
        EconomicCalendarId entityId,
        Guid id,
        DateTime errorDate,
        long eventId,
        Guid commandId,
        string eventSource,
        string errorMessage,
        int errorCode,
        ErrorType errorType,
        string errorData,
        DateTime receivedOn,
        string aggregateId,
        string commandName,
        string commandData,
        string routeTo,
        DateTime importedDate,
        string[] countryCodes,
        ImportDuplicatePolicy duplicatePolicy,
        TomasAI.IFM.Domain.MarketData.Shared.DownloadLog.MarketDataDownloadOutcome? downloadOutcome = null)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        ErrorDate = errorDate;
        EventId = eventId;
        CommandId = commandId;
        EventSource = eventSource ?? string.Empty;
        ErrorMessage = errorMessage ?? string.Empty;
        ErrorCode = errorCode;
        ErrorType = errorType;
        ErrorData = errorData ?? string.Empty;
        ReceivedOn = receivedOn;
        AggregateId = aggregateId ?? string.Empty;
        CommandName = commandName ?? string.Empty;
        CommandData = commandData ?? string.Empty;
        RouteTo = routeTo ?? string.Empty;
        ImportedDate = importedDate;
        CountryCodes = countryCodes ?? [];
        DuplicatePolicy = duplicatePolicy;
        DownloadOutcome = downloadOutcome;
    }
}
