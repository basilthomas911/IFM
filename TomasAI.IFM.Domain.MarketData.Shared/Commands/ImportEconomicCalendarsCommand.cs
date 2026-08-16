using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Commands;

/// <summary>
/// Command to acquire and import economic calendar entries for a given date and optional country filters.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands (base keys 0�5; custom properties start at key 6).
/// Routes to <see cref="BoundedContextName.EconomicCalendarBoundedContext"/> with error code 10035.
/// The entity id groups the import operation under a synthetic identifier (ImportEconomicCalendars).
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ImportEconomicCalendarsCommand : ICommand<EconomicCalendarId>
{
    /// <summary>Actor domain name (excluded from serialization).</summary>
    [IgnoreMember]
    public const string Actor = "EconomicCalendarCommand";

    /// <summary>Command verb (excluded from serialization).</summary>
    [IgnoreMember]
    public const string Verb = "Import";

    /// <summary>Error code for this command (excluded from serialization).</summary>
    [IgnoreMember]
    public const int ErrorId = 10035;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public EconomicCalendarId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Date associated with this import batch (used to build the entity id).</summary>
    [Key(7)]
    public DateTime ImportedDate { get; init; }

    [Key(8)]
    public ImportDuplicatePolicy DuplicatePolicy { get; init; } = ImportDuplicatePolicy.Overwrite;

    /// <summary>Optional country filters for the external acquisition.</summary>
    [Key(9)]
    public string[] CountryCodes { get; init; } = [];

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ImportEconomicCalendarsCommand() { }

    /// <summary>
    /// Creates a new command to import economic calendar entries.
    /// </summary>
    /// <param name="importedDate">Import batch date.</param>
    public ImportEconomicCalendarsCommand(
        DateTime importedDate,
        string[]? countryCodes = null,
        ImportDuplicatePolicy duplicatePolicy = ImportDuplicatePolicy.Overwrite)
    {
        ImportedDate = importedDate;
        CountryCodes = countryCodes ?? [];
        DuplicatePolicy = duplicatePolicy;

        EntityId = new EconomicCalendarId(ImportedDate, "ZZ", "ImportEconomicCalendars");
        RouteTo = BoundedContextName.EconomicCalendarBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public ImportEconomicCalendarsCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        EconomicCalendarId entityId,    // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        DateTime importedDate,          // Key(7)
        ImportDuplicatePolicy duplicatePolicy, // Key(8)
        string[] countryCodes)          // Key(9)
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
