using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Commands;

/// <summary>
/// Command to add a new economic calendar entry.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.EconomicCalendarBoundedContext"/> with error code 10003.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record AddEconomicCalendarCommand : ICommand<EconomicCalendarId>
{
    [IgnoreMember] public const string Actor = "EconomicCalendarCommand";
    [IgnoreMember] public const string Verb = "Add";

    /// <summary>Error code for this command (excluded from serialization).</summary>
    [IgnoreMember]
    public const int ErrorId = 10003;

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

    /// <summary>The economic calendar view model to add.</summary>
    [Key(6)]
    public EconomicCalendarReadModel EconomicCalendar { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public AddEconomicCalendarCommand() { }

    /// <summary>
    /// Creates a new command to add an economic calendar entry.
    /// </summary>
    /// <param name="economicCalendar">Economic calendar view model (cannot be null).</param>
    public AddEconomicCalendarCommand(EconomicCalendarReadModel economicCalendar)
    {
        EconomicCalendar = economicCalendar;

        EntityId = EconomicCalendar.Id;
        RouteTo = BoundedContextName.EconomicCalendarBoundedContext;
        ErrorCode = 10003;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public AddEconomicCalendarCommand(
        Guid commandId,                  // Key(0)
        ActorSubject subject,            // Key(1)
        bool postEvents,                 // Key(2)
        EconomicCalendarId entityId,     // Key(3)
        int errorCode,                   // Key(4)
        BoundedContextName routeTo,      // Key(5)
        EconomicCalendarReadModel economicCalendar) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        EconomicCalendar = economicCalendar;
    }
}