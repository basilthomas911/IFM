using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to reset the market data feed for the specified futures contracts and value date.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. Routes to
/// <see cref="BoundedContextName.MarketDataFeedBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0�5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ResetMarketDataFeedCommand : ICommand<MarketDataFeedId>
{
    public const string Actor = "MarketDataFeedCommand";
    public const string Verb = "Reset";
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

    /// <summary>
    /// The collection of futures contracts to reset in the market data feed.
    /// </summary>
    [Key(6)]
    public FuturesContractV2ReadModel[] FuturesContracts { get; init; }

    /// <summary>
    /// The target value date for which the feed reset should be applied.
    /// </summary>
    [Key(7)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ResetMarketDataFeedCommand() { }

    /// <summary>
    /// Creates a new command to reset the market data feed for the specified contracts and value date.
    /// </summary>
    /// <param name="futuresContracts">Array of futures contracts (cannot be null).</param>
    /// <param name="valueDate">The target value date.</param>
    public ResetMarketDataFeedCommand(FuturesContractV2ReadModel[] futuresContracts, DateOnly valueDate)
    {
        FuturesContracts = futuresContracts ?? throw new ArgumentNullException(nameof(futuresContracts));
        ValueDate = valueDate;

        EntityId = new MarketDataFeedId(ValueDate);
        RouteTo = BoundedContextName.MarketDataFeedBoundedContext;
        ErrorCode = 5005;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public ResetMarketDataFeedCommand(
        Guid commandId,                          // Key(0)
        ActorSubject subject,                    // Key(1)
        bool postEvents,                         // Key(2)
        MarketDataFeedId entityId,               // Key(3)
        int errorCode,                           // Key(4)
        BoundedContextName routeTo,              // Key(5)
        FuturesContractV2ReadModel[] contracts,  // Key(6)
        DateOnly valueDate)                      // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FuturesContracts = contracts;
        ValueDate = valueDate;
    }
}