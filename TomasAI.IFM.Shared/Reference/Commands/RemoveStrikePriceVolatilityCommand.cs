using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.Commands;

/// <summary>
/// Command to remove a strike price volatility entry identified by its unique ID.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.StrikePriceVolatilityBoundedContext"/> with error code 8006.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record RemoveStrikePriceVolatilityCommand
    : ICommand<StrikePriceVolatilityId>
{
    public const string Actor = "StrikePriceVolatilityCommand";
    public const string Verb = "Remove";
    public const int ErrorId = 8006;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public StrikePriceVolatilityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The identifier of the strike price volatility entry to remove.</summary>
    [Key(6)]
    public StrikePriceVolatilityId StrikePriceVolatilityId { get; init; }

    /// <summary>True to force/overwrite removal where applicable; otherwise false.</summary>
    [Key(7)]
    public bool Overwrite { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public RemoveStrikePriceVolatilityCommand() { }

    /// <summary>
    /// Creates a new command to remove a strike price volatility entry.
    /// </summary>
    /// <param name="strikePriceVolatilityId">Target strike price volatility identifier.</param>
    /// <param name="overwrite">Set true to force/overwrite related persisted data.</param>
    public RemoveStrikePriceVolatilityCommand(StrikePriceVolatilityId strikePriceVolatilityId, bool overwrite = false)
    {
        StrikePriceVolatilityId = strikePriceVolatilityId;
        Overwrite = overwrite;

        EntityId = StrikePriceVolatilityId;
        RouteTo = BoundedContextName.StrikePriceVolatilityBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public RemoveStrikePriceVolatilityCommand(
        Guid commandId,                      // Key(0)
        ActorSubject subject,                // Key(1)
        bool postEvents,                     // Key(2)
        StrikePriceVolatilityId entityId,    // Key(3)
        int errorCode,                       // Key(4)
        BoundedContextName routeTo,          // Key(5)
        StrikePriceVolatilityId strikePriceVolatilityId, // Key(6)
        bool overwrite)                      // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        StrikePriceVolatilityId = strikePriceVolatilityId;
        Overwrite = overwrite;
    }
}