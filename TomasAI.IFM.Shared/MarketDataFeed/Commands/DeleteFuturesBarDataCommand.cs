using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to delete a specific FuturesBarData entity identified by its unique ID.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. The command is routed to
/// <see cref="BoundedContextName.FuturesBarDataBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0–5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record DeleteFuturesBarDataCommand : ICommand<FuturesBarDataId>
{
    public const string Actor = "FuturesBarDataCommand";
    public const string Verb = "Delete";
    public const int ErrorId = 5003;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesBarDataId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// The unique identifier of the FuturesBarData entity to be deleted.
    /// </summary>
    [Key(6)]
    public FuturesBarDataId Id { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public DeleteFuturesBarDataCommand() { }

    /// <summary>
    /// Creates a new delete command for the specified FuturesBarData entity.
    /// </summary>
    /// <param name="entityId">The unique identifier of the FuturesBarData entity.</param>
    public DeleteFuturesBarDataCommand(FuturesBarDataId entityId)
    {
        Id = entityId ?? throw new ArgumentNullException(nameof(entityId));
        EntityId = Id;
        ErrorCode = 5003;
        RouteTo = BoundedContextName.FuturesBarDataBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public DeleteFuturesBarDataCommand(
        Guid commandId,                    // Key(0)
        ActorSubject subject,              // Key(1)
        bool postEvents,                   // Key(2)
        FuturesBarDataId entityId,         // Key(3)
        int errorCode,                     // Key(4)
        BoundedContextName routeTo,        // Key(5)
        FuturesBarDataId id)               // Key(6)
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