using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer.Commands;

/// <summary>
/// Command to clear (reset/remove) a spread distribution job associated with a specific option trade.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command members occupy keys 0–5 in <see cref="BaseCommand{TEntityId}"/>; 
/// custom properties start at key 6. Routes to <see cref="BoundedContextName.SpreadDistributionJobBoundedContext"/> 
/// and uses error code 7005.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ClearSpreadDistributionJobCommand
    : ICommand<SpreadDistributionJobEntityId>
{
    public const string Actor = "SpreadDistributionJobCommand";
    public const string Verb = "Clear";
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

    [IgnoreMember] public string CommandName => nameof(ClearSpreadDistributionJobCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => EventSourceValue;
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => OriginatedByValue;

    /// <summary>Parameterless constructor required for MessagePack deserialization.</summary>
    public ClearSpreadDistributionJobCommand() { }

    /// <summary>
    /// Creates a new command to clear the spread distribution job for the specified option trade.
    /// </summary>
    /// <param name="spreadDistributionJobId">The spread distribution job identifier (cannot be null).</param>
    public ClearSpreadDistributionJobCommand(SpreadDistributionJobEntityId entityId)
    {
        EntityId = entityId;
        RouteTo = BoundedContextName.SpreadDistributionJobBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public ClearSpreadDistributionJobCommand(
        Guid commandId,                // Key(0)
        ActorSubject subject,          // Key(1)
        bool postEvents,               // Key(2)
        SpreadDistributionJobEntityId entityId,  // Key(3)
        int errorCode,                 // Key(4)
        BoundedContextName routeTo) // Key(5)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
    }
}