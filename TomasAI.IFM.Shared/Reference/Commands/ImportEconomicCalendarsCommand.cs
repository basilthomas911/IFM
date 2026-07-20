using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Commands;

/// <summary>
/// Command to import a batch of economic calendar entries for a given import date.
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

    /// <summary>Collection of economic calendar entries to import.</summary>
    [Key(6)]
    public EconomicCalendarReadModel[] EconomicCalendars { get; init; } = [];

    /// <summary>Date associated with this import batch (used to build the entity id).</summary>
    [Key(7)]
    public DateTime ImportedDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ImportEconomicCalendarsCommand() { }

    /// <summary>
    /// Creates a new command to import economic calendar entries.
    /// </summary>
    /// <param name="economicCalendars">Array of calendar entries (cannot be null).</param>
    /// <param name="importedDate">Import batch date.</param>
    public ImportEconomicCalendarsCommand(EconomicCalendarReadModel[] economicCalendars, DateTime importedDate)
    {
        EconomicCalendars = economicCalendars ?? throw new ArgumentNullException(nameof(economicCalendars));
        ImportedDate = importedDate;

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
        EconomicCalendarReadModel[] economicCalendars, // Key(6)
        DateTime importedDate)          // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        EconomicCalendars = economicCalendars ?? [];
        ImportedDate = importedDate;
    }
}