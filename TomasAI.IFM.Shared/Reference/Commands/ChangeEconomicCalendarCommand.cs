using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Commands;

/// <summary>
/// Command to change (update) an existing economic calendar entry.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.EconomicCalendarBoundedContext"/> with error code 10004.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ChangeEconomicCalendarCommand : ICommand<EconomicCalendarId>
{
    /// <summary>Actor domain name (excluded from serialization).</summary>
    [IgnoreMember]
    public const string Actor = "EconomicCalendarCommand";

    /// <summary>Command verb (excluded from serialization).</summary>
    [IgnoreMember]
    public const string Verb = "Change";

    /// <summary>Error code for this command (excluded from serialization).</summary>
    [IgnoreMember]
    public const int ErrorId = 10004;

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

    /// <summary>The identifier of the economic calendar entry to update.</summary>
    [Key(6)]
    public EconomicCalendarId EconomicCalendarId { get; init; }

    /// <summary>The updated economic calendar view model payload.</summary>
    [Key(7)]
    public EconomicCalendarReadModel EconomicCalendar { get; init; }

    /// <summary>True to overwrite existing data; otherwise false.</summary>
    [Key(8)]
    public bool Overwrite { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ChangeEconomicCalendarCommand() { }

    /// <summary>
    /// Creates a new command to update an economic calendar entry.
    /// </summary>
    /// <param name="economicCalendarId">Target economic calendar identifier.</param>
    /// <param name="economicCalendar">Updated economic calendar values (cannot be null).</param>
    /// <param name="overwrite">Set true to overwrite existing data.</param>
    public ChangeEconomicCalendarCommand(
        EconomicCalendarId economicCalendarId,
        EconomicCalendarReadModel economicCalendar,
        bool overwrite = false)
    {
        EconomicCalendarId = economicCalendarId;
        EconomicCalendar = economicCalendar ?? throw new ArgumentNullException(nameof(economicCalendar));
        Overwrite = overwrite;

        EntityId = economicCalendarId;
        RouteTo = BoundedContextName.EconomicCalendarBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public ChangeEconomicCalendarCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        EconomicCalendarId entityId,    // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        EconomicCalendarId economicCalendarId, // Key(6)
        EconomicCalendarReadModel economicCalendar, // Key(7)
        bool overwrite)                 // Key(8)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        EconomicCalendarId = economicCalendarId;
        EconomicCalendar = economicCalendar;
        Overwrite = overwrite;
    }
}