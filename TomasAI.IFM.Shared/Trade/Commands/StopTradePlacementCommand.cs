using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to stop a trade placement operation for a given trade placement identifier.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom members start at key 6.
/// Routes to <see cref="BoundedContextName.TradePlacementBoundedContext"/> with error code 4037.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StopTradePlacementCommand
    : ICommand<TradePlacementId>
{
    public const string Actor = "TradePlacementCommand";
    public const string Verb = "Stop";
    public const int ErrorId = 4037;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public TradePlacementId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The trade placement identifier.</summary>
    [Key(6)]
    public TradePlacementId Id { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public StopTradePlacementCommand() { }

    /// <summary>
    /// Creates a new command to stop trade placement.
    /// </summary>
    /// <param name="id">The trade placement identifier.</param>
    public StopTradePlacementCommand(TradePlacementId id)
    {
        Id = id;

        EntityId = Id;
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.TradePlacementBoundedContext;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> assignments).
    /// </summary>
    [SerializationConstructor]
    public StopTradePlacementCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        TradePlacementId entityId,      // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        TradePlacementId id)            // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Id = id;
    }
}