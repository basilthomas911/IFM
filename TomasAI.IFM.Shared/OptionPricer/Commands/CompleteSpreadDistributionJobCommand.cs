using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer.Commands;

/// <summary>
/// Command to mark a spread distribution job as completed.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern: base command keys 0–5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.SpreadDistributionJobBoundedContext"/> with error code 7005.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record CompleteSpreadDistributionJobCommand
    : ICommand<SpreadDistributionJobEntityId>
{
    public const string Actor = "SpreadDistributionJobCommand";
    public const string Verb = "Complete";
    public const int ErrorId = 7005;
    const string EventSourceValue = Actor + "Actor";
    static readonly string OriginatedByValue = string.Concat(Environment.UserDomainName, "\\", Environment.UserName);

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public SpreadDistributionJobEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members

    [IgnoreMember] public string CommandName => nameof(CompleteSpreadDistributionJobCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => EventSourceValue;
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => OriginatedByValue;

    /// <summary>The timestamp when the job was completed.</summary>
    [Key(6)]
    public DateTime JobCompleted { get; init; }

    /// <summary>The final status of the spread distribution job.</summary>
    [Key(7)]
    public SpreadDistributionJobStatus JobStatus { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public CompleteSpreadDistributionJobCommand() { }

    /// <summary>
    /// Creates a new command to finalize a spread distribution job.
    /// </summary>
    /// <param name="optionTradeId">Option trade identifier.</param>
    /// <param name="jobId">Job identifier.</param>
    /// <param name="jobCompleted">Completion timestamp.</param>
    /// <param name="jobStatus">Final job status.</param>
    public CompleteSpreadDistributionJobCommand(
        SpreadDistributionJobEntityId entityId,
        DateTime jobCompleted,
        SpreadDistributionJobStatus jobStatus)
    {
        JobCompleted = jobCompleted;
        JobStatus = jobStatus;

        EntityId = entityId;
        RouteTo = BoundedContextName.SpreadDistributionJobBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public CompleteSpreadDistributionJobCommand(
        Guid commandId,                   // Key(0)
        ActorSubject subject,             // Key(1)
        bool postEvents,                  // Key(2)
        SpreadDistributionJobEntityId entityId,     // Key(3)
        int errorCode,                    // Key(4)
        BoundedContextName routeTo,       // Key(5)
        DateTime jobCompleted,            // Key(8)
        SpreadDistributionJobStatus jobStatus) // Key(9)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        JobCompleted = jobCompleted;
        JobStatus = jobStatus;
    }
}