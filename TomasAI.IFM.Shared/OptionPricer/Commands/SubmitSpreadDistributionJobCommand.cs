using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer.Commands;

/// <summary>
/// Command to submit a spread distribution job for processing.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.SpreadDistributionJobBoundedContext"/> with error code 7005.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record SubmitSpreadDistributionJobCommand : ICommand<SpreadDistributionJobEntityId>
{
    public const string Actor = "SpreadDistributionJobCommand";
    public const string Verb = "Submit";
    public const int ErrorId = 7005;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public SpreadDistributionJobEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The spread distribution job payload to be submitted.</summary>
    [Key(6)]
    public SpreadDistributionJobReadModel SpreadDistributionJob { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public SubmitSpreadDistributionJobCommand() { }

    /// <summary>
    /// Creates a new command to submit a spread distribution job.
    /// </summary>
    /// <param name="spreadDistributionJob">The job details to submit (cannot be null).</param>
    public SubmitSpreadDistributionJobCommand(SpreadDistributionJobReadModel spreadDistributionJob)
    {
        SpreadDistributionJob = spreadDistributionJob;

        EntityId = SpreadDistributionJob.EntityId;
        RouteTo = BoundedContextName.SpreadDistributionJobBoundedContext;
        ErrorCode = 7005;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public SubmitSpreadDistributionJobCommand(
        Guid commandId,                      // Key(0)
        ActorSubject subject,                // Key(1)
        bool postEvents,                     // Key(2)
        SpreadDistributionJobEntityId entityId,        // Key(3)
        int errorCode,                       // Key(4)
        BoundedContextName routeTo,          // Key(5)
        SpreadDistributionJobReadModel spreadDistributionJob) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        SpreadDistributionJob = spreadDistributionJob;
    }
}