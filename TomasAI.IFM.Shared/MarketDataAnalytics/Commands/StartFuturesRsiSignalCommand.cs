using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Command to start (initialize) a futures RSI signal workflow for the specified RSI signal entity.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other analytics commands. Routes to
/// <see cref="BoundedContextName.FuturesRsiSignalBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0–5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StartFuturesRsiSignalCommand : ICommand<FuturesRsiSignalEntityId>
{
    public const string Actor = "FuturesRsiSignalCommand";
    public const string Verb = "Start";
    public const int ErrorId = 20002;

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
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public StartFuturesRsiSignalCommand() { }

    /// <summary>
    /// Creates a new command to start processing for the specified RSI signal entity.
    /// </summary>
    /// <param name="id">RSI signal entity identifier (cannot be null).</param>
    public StartFuturesRsiSignalCommand(FuturesRsiSignalEntityId id)
    {
        EntityId = id;
        ErrorCode = 20002;
        RouteTo = BoundedContextName.FuturesRsiSignalBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public StartFuturesRsiSignalCommand(
        Guid commandId,                   // Key(0)
        ActorSubject subject,             // Key(1)
        bool postEvents,                  // Key(2)
        FuturesRsiSignalEntityId entityId,// Key(3)
        int errorCode,                    // Key(4)
        BoundedContextName routeTo)      // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
    }
}