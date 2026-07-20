using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Commands;

/// <summary>
/// Command to add (register) a new scheduled job definition.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.SystemAdminBoundedContext"/> with error code 8003.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record AddScheduledJobCommand : ICommand<ScheduledJobId>
{
    /// <summary>Actor domain name (excluded from serialization).</summary>
    [IgnoreMember]
    public const string Actor = "ScheduledJobCommand";

    /// <summary>Verb describing the action (excluded from serialization).</summary>
    [IgnoreMember]
    public const string Verb = "Add";

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

    /// <summary>Scheduled job view model payload.</summary>
    [Key(6)]
    public ScheduledJobReadModel ScheduledJob { get; init; }

    /// <summary>Parameterless constructor required for MessagePack deserialization.</summary>
    public AddScheduledJobCommand() { }

    /// <summary>
    /// Creates a new command to add a scheduled job.
    /// </summary>
    /// <param name="scheduledJob">Job definition (cannot be null).</param>
    public AddScheduledJobCommand(ScheduledJobReadModel scheduledJob)
    {
        ScheduledJob = scheduledJob ?? throw new ArgumentNullException(nameof(scheduledJob));

        EntityId = ScheduledJob.Id;
        RouteTo = BoundedContextName.SystemAdminBoundedContext;
        ErrorCode = 8003;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public AddScheduledJobCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        ScheduledJobId entityId,         // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        ScheduledJobReadModel scheduledJob) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ScheduledJob = scheduledJob;
    }
}