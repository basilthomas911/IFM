using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Telemetry.ViewModels;

namespace TomasAI.IFM.Shared.Telemetry.Commands;

/// <summary>
/// Command to add a batch of telemetry log events.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.TelemetryLogsBoundedContext"/> with error code 4006.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record AddLogEventsCommand
    : ICommand<LogEventsId>
{
    public const string Actor = "TelemetryLogsCommand";
    public const string Verb = "Add";
    public const int ErrorId = 4006;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public LogEventsId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Telemetry log events payload.</summary>
    [Key(6)]
    public LogEventsReadModel LogEvents { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public AddLogEventsCommand() { }

    /// <summary>
    /// Creates a new command to add telemetry log events.
    /// </summary>
    /// <param name="logEvents">Log events payload (cannot be null).</param>
    public AddLogEventsCommand(LogEventsReadModel logEvents)
    {
        LogEvents = logEvents ?? throw new ArgumentNullException(nameof(logEvents));

        EntityId = LogEvents.Id;
        RouteTo = BoundedContextName.TelemetryLogsBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public AddLogEventsCommand(
        Guid commandId,             // Key(0)
        ActorSubject subject,       // Key(1)
        bool postEvents,            // Key(2)
        LogEventsId entityId,       // Key(3)
        int errorCode,              // Key(4)
        BoundedContextName routeTo, // Key(5)
        LogEventsReadModel logEvents) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        LogEvents = logEvents;
    }
}