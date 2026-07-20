using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Commands;

/// <summary>
/// Command to remove (delete) a scheduled job identified by its unique name.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern: base command keys 0–5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.SystemAdminBoundedContext"/> with error code 8004.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record RemoveScheduledJobCommand
    : ICommand<ScheduledJobId>
{
    public const string Actor = "ScheduledJobCommand";
    public const string Verb = "Remove";
    public const int ErrorId = 8004;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public ScheduledJobId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Gets or sets the identifier for the scheduled job.
    /// </summary>
    [Key(6)]
    public ScheduledJobId ScheduledJobId { get; init; } 

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public RemoveScheduledJobCommand() { }

    /// <summary>
    /// Creates a new command to remove the specified scheduled job.
    /// </summary>
    /// <param name="scheduledJobName">Scheduled job name (cannot be null or empty).</param>
    public RemoveScheduledJobCommand(ScheduledJobId scheduledJobId  )
    {
        EntityId = scheduledJobId;
        RouteTo = BoundedContextName.SystemAdminBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public RemoveScheduledJobCommand(
        Guid commandId,              // Key(0)
        ActorSubject subject,        // Key(1)
        bool postEvents,             // Key(2)
        ScheduledJobId entityId,      // Key(3)
        int errorCode,               // Key(4)
        BoundedContextName routeTo,  // Key(5)
        ScheduledJobId scheduledJobId)     // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ScheduledJobId = scheduledJobId;
    }
}