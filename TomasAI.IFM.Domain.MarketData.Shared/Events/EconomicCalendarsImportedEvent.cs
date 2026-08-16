using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>
/// Represents an accepted request to acquire and import economic calendars.
/// </summary>
/// <remarks>The event carries acquisition parameters and correlation metadata only. Its event-family handler acquires,
/// validates, and stores the records before publishing a complete or fail terminal event.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record EconomicCalendarsImportedEvent : IEvent<EconomicCalendarId>
{
    [IgnoreMember] public const string Actor = "EconomicCalendarEvent";
    [IgnoreMember] public const string Verb = "Imported";
    [IgnoreMember] public const int ErrorCode = 7035;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public EconomicCalendarId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(9)] public DateTime RequestedOn { get; init; }
    [Key(10)] public string RequestedBy { get; init; }
    [Key(11)] public ImportDuplicatePolicy DuplicatePolicy { get; init; } = ImportDuplicatePolicy.Overwrite;
    [Key(12)] public DateTime ImportedDate { get; init; }
    [Key(13)] public string[] CountryCodes { get; init; } = [];

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public EconomicCalendarsImportedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public EconomicCalendarsImportedEvent(
        ActorSubject subject,
        Guid id,
        EconomicCalendarId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateTime requestedOn,
        string requestedBy,
        ImportDuplicatePolicy duplicatePolicy,
        DateTime importedDate,
        string[] countryCodes)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        RequestedOn = requestedOn;
        RequestedBy = requestedBy ?? string.Empty;
        DuplicatePolicy = duplicatePolicy;
        ImportedDate = importedDate;
        CountryCodes = countryCodes ?? [];
    }

    /// <summary>
    /// Converts this request event into a zero-record completed event.
    /// Validates the requested entity id type and returns a strongly-typed complete event.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
        => ToCompleteEvent<TComplete, TEntityId>([]);

    /// <summary>
    /// Converts this import request into its successful terminal event using the records actually acquired and stored.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>(
        EconomicCalendarReadModel[] economicCalendars)
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(economicCalendars);
        if (typeof(TEntityId) != typeof(EconomicCalendarId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(EconomicCalendarId).FullName}.");

        ICompleteEvent<EconomicCalendarId> completed = new EconomicCalendarsImportedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, EconomicCalendarsImportedCompleteEvent.Actor, EconomicCalendarsImportedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            EconomicCalendars = economicCalendars,
            ImportedOn = DateTime.UtcNow,
            ImportedBy = this.RequestedBy,
            DuplicatePolicy = this.DuplicatePolicy,
            ImportedDate = this.ImportedDate,
            CountryCodes = this.CountryCodes
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// Validates the requested entity id type and returns a strongly-typed error event.
    /// </summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(EconomicCalendarId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(EconomicCalendarId).FullName}.");

        IErrorEvent<EconomicCalendarId> failed = new EconomicCalendarsImportedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, EconomicCalendarsImportedFailEvent.Actor, EconomicCalendarsImportedFailEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            ErrorDate = DateTime.UtcNow,
            EventId = this.EventId,
            CommandId = this.CommandId == Guid.Empty ? Guid.NewGuid() : this.CommandId,
            EventSource = this.EventSource,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode,
            ErrorData = ex.ToString(),
            ReceivedOn = this.ReceivedOn,
            AggregateId = this.AggregateId,
            ImportedDate = this.ImportedDate,
            CountryCodes = this.CountryCodes,
            DuplicatePolicy = this.DuplicatePolicy
        };

        return (IErrorEvent<TEntityId>)failed;
    }
}

/// <summary>
/// Event published when economic calendars have been imported successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record EconomicCalendarsImportedCompleteEvent : ICompleteEvent<EconomicCalendarId>
{
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

    [Key(8)] public EconomicCalendarReadModel[] EconomicCalendars { get; init; }
    [Key(9)] public DateTime ImportedOn { get; init; }
    [Key(10)] public string ImportedBy { get; init; }
    [Key(11)] public ImportDuplicatePolicy DuplicatePolicy { get; init; } = ImportDuplicatePolicy.Overwrite;
    [Key(12)] public DateTime ImportedDate { get; init; }
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
        string[] countryCodes)
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
    }
}

/// <summary>
/// Event published when importing economic calendars fails.
/// Carries standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record EconomicCalendarsImportedFailEvent : IErrorEvent<EconomicCalendarId>
{
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
        ImportDuplicatePolicy duplicatePolicy)
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
    }
}
