using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Shared.OptionPricer.Commands;

/// <summary>
/// Command to insert (persist) a put and call spread distribution snapshot for a trade.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// EntityId is derived from the put distribution (trade scope + value date + days to expiry).
/// Routes to <see cref="BoundedContextName.SpreadDistributionJobBoundedContext"/> with error code 7001.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record InsertSpreadDistributionCommand : ICommand<SpreadDistributionEntityId>
{
    public const string Actor = "SpreadDistributionCommand";
    public const string Verb = "Insert";
    public const int ErrorId = 7001;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } 
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public SpreadDistributionEntityId EntityId { get; init; } 
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The put side spread distribution snapshot.</summary>
    [Key(6)]
    public SpreadDistributionReadModel PutSpreadDistribution { get; init; }

    /// <summary>The call side spread distribution snapshot.</summary>
    [Key(7)]
    public SpreadDistributionReadModel CallSpreadDistribution { get; init; }

    /// <summary>Parameterless constructor required for MessagePack deserialization.</summary>
    public InsertSpreadDistributionCommand() { }

    /// <summary>
    /// Creates a new command to insert paired put/call spread distributions for the same trade context.
    /// </summary>
    /// <param name="putSpreadDistribution">Put distribution (cannot be null).</param>
    /// <param name="callSpreadDistribution">Call distribution (cannot be null).</param>
    public InsertSpreadDistributionCommand(
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution)
    {
        PutSpreadDistribution = putSpreadDistribution ?? throw new ArgumentNullException(nameof(putSpreadDistribution));
        CallSpreadDistribution = callSpreadDistribution ?? throw new ArgumentNullException(nameof(callSpreadDistribution));

        EntityId = new SpreadDistributionEntityId(
            TradeId: PutSpreadDistribution.TradeId,
            ValueDate: PutSpreadDistribution.ValueDate);
        RouteTo = BoundedContextName.SpreadDistributionJobBoundedContext;
        ErrorCode = 7001;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public InsertSpreadDistributionCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        SpreadDistributionEntityId entityId, // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        SpreadDistributionReadModel putSpreadDistribution,  // Key(6)
        SpreadDistributionReadModel callSpreadDistribution) // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        PutSpreadDistribution = putSpreadDistribution;
        CallSpreadDistribution = callSpreadDistribution;
    }
}