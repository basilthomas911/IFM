using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to remove all live trade feeds associated with a specific order.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. Routes to
/// <see cref="BoundedContextName.MarketDataFeedBoundedContext"/>. Custom property keys start at 6 because
/// base command members occupy keys 0–5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record RemoveTradeLiveFeedsCommand 
    : ICommand<TradeLiveFeedsId>
{
    public const string Actor = "MarketDataFeedCommand";
    public const string Verb = "RemoveTradeLiveFeeds";
    public const int ErrorId = 4021;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public TradeLiveFeedsId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// The unique identifier of the order whose live trade feeds should be removed.
    /// </summary>
    [Key(6)]
    public int OrderId { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public RemoveTradeLiveFeedsCommand() { }

    /// <summary>
    /// Creates a new command to remove all live trade feeds for the specified order.
    /// </summary>
    /// <param name="orderId">Order identifier.</param>
    public RemoveTradeLiveFeedsCommand(int orderId)
    {
        OrderId = orderId;
        EntityId = new TradeLiveFeedsId(orderId);
        RouteTo = BoundedContextName.MarketDataFeedBoundedContext;
        ErrorCode = 4021;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public RemoveTradeLiveFeedsCommand(
        Guid commandId,            // Key(0)
        ActorSubject subject,      // Key(1)
        bool postEvents,           // Key(2)
        TradeLiveFeedsId entityId, // Key(3)
        int errorCode,             // Key(4)
        BoundedContextName routeTo,// Key(5)
        int orderId)                // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        OrderId = orderId;
    }
}