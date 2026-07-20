using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Command to stop a futures RSI signal workflow for the specified RSI signal entity.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other analytics commands. Routes to
/// <see cref="BoundedContextName.FuturesRsiSignalBoundedContext"/>. Custom properties start at key index 6
/// because base command members occupy keys 0–5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StopFuturesRsiSignalCommand : ICommand<FuturesRsiSignalEntityId>
{
    public const string Actor = "FuturesRsiSignalCommand";
    public const string Verb = "Stop";
    public const int ErrorId = 20004;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesRsiSignalEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// The target RSI signal entity identifier (contract + value date).
    /// </summary>
    [Key(6)]
    public FuturesRsiSignalEntityId Id { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public StopFuturesRsiSignalCommand() { }

    /// <summary>
    /// Creates a new command to stop processing for the specified RSI signal entity.
    /// </summary>
    /// <param name="id">RSI signal entity identifier (cannot be null).</param>
    public StopFuturesRsiSignalCommand(FuturesRsiSignalEntityId id)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        EntityId = Id;
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FuturesRsiSignalBoundedContext;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters align with key order 0–6.
    /// </summary>
    [SerializationConstructor]
    public StopFuturesRsiSignalCommand(
        Guid commandId,                   // Key(0)
        ActorSubject subject,             // Key(1)
        bool postEvents,                  // Key(2)
        FuturesRsiSignalEntityId entityId,// Key(3)
        int errorCode,                    // Key(4)
        BoundedContextName routeTo,       // Key(5)
        FuturesRsiSignalEntityId id)      // Key(6)
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