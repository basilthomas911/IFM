using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to stop the market data feed for a specific value (trading) date.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern (base command keys 0–5). Custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.MarketDataFeedBoundedContext"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StopMarketDataFeedCommand : ICommand<MarketDataFeedId>
{
    public const string Actor = "MarketDataFeedCommand";
    public const string Verb = "Stop";
    public const int ErrorId = 5005;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public MarketDataFeedId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The value (trading) date for which the market data feed should be stopped.</summary>
    [Key(6)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public StopMarketDataFeedCommand() { }

    /// <summary>
    /// Creates a new command to stop the market data feed for the specified value date.
    /// </summary>
    /// <param name="valueDate">The target value (trading) date.</param>
    public StopMarketDataFeedCommand(DateOnly valueDate)
    {
        ValueDate = valueDate;

        EntityId = new MarketDataFeedId(ValueDate);
        RouteTo = BoundedContextName.MarketDataFeedBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters align with key order 0–6.
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Indicates whether to publish resulting events (key 2).</param>
    /// <param name="entityId">Market data feed entity identifier (key 3).</param>
    /// <param name="errorCode">Associated error code (key 4).</param>
    /// <param name="routeTo">Target bounded context (key 5).</param>
    /// <param name="valueDate">The value date (key 6).</param>
    [SerializationConstructor]
    public StopMarketDataFeedCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        MarketDataFeedId entityId,      // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        DateOnly valueDate)             // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ValueDate = valueDate;
    }
}