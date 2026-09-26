using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Commands;

/// <summary>Economic-calendar import with contiguous actor-command wire keys.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ImportEconomicCalendarsCommand : ICommand<EconomicCalendarId>
{
    /// <summary>The owning command actor.</summary>
    [IgnoreMember] public const string Actor = "EconomicCalendarCommand";
    /// <summary>The command verb.</summary>
    [IgnoreMember] public const string Verb = "Import";
    /// <summary>The stable error code.</summary>
    [IgnoreMember] public const int ErrorId = 10035;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public EconomicCalendarId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    [Key(6)] public DateTime ImportedDate { get; init; }
    [Key(7)] public ImportDuplicatePolicy DuplicatePolicy { get; init; } = ImportDuplicatePolicy.Overwrite;
    [Key(8)] public string[] CountryCodes { get; init; } = [];

    [IgnoreMember] public string CommandName => nameof(ImportEconomicCalendarsCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Creates an empty command for serialization.</summary>
    public ImportEconomicCalendarsCommand() { }

    /// <summary>Creates a calendar import with the established application defaults.</summary>
    /// <param name="importedDate">The date being imported.</param>
    /// <param name="countryCodes">Optional country filters.</param>
    /// <param name="duplicatePolicy">Duplicate handling policy.</param>
    public ImportEconomicCalendarsCommand(DateTime importedDate, string[]? countryCodes = null,
        ImportDuplicatePolicy duplicatePolicy = ImportDuplicatePolicy.Overwrite)
    {
        ImportedDate = importedDate;
        CountryCodes = countryCodes ?? [];
        DuplicatePolicy = duplicatePolicy;
        EntityId = new EconomicCalendarId(importedDate, "ZZ", "ImportEconomicCalendars");
        RouteTo = BoundedContextName.EconomicCalendarBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>Rehydrates the contiguous wire fields in permanent numeric-key order.</summary>
    /// <param name="commandId">The command identifier.</param>
    /// <param name="subject">The actor subject.</param>
    /// <param name="postEvents">Whether to publish domain events.</param>
    /// <param name="entityId">The calendar import identity.</param>
    /// <param name="errorCode">The command error code.</param>
    /// <param name="routeTo">The bounded context route.</param>
    /// <param name="importedDate">The import date.</param>
    /// <param name="duplicatePolicy">The duplicate policy.</param>
    /// <param name="countryCodes">The country filters.</param>
    [SerializationConstructor]
    public ImportEconomicCalendarsCommand(Guid commandId, ActorSubject subject, bool postEvents,
        EconomicCalendarId entityId, int errorCode, BoundedContextName routeTo, DateTime importedDate,
        ImportDuplicatePolicy duplicatePolicy, string[] countryCodes)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ImportedDate = importedDate;
        DuplicatePolicy = duplicatePolicy;
        CountryCodes = countryCodes ?? [];
    }
}
