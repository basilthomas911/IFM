using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to start streaming futures bar data for the specified set of contracts on a given value date.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands (base command keys 0�5).
/// Custom properties begin at key index 6. Routes to <see cref="BoundedContextName.FuturesBarDataBoundedContext"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StartFuturesBarDataStreamingCommand : ICommand<FuturesBarDataStreamingId>
{
    public const string Actor = "FuturesBarDataCommand";
    public const string Verb = "StartStreaming";
    public const int ErrorId = 5004;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesBarDataStreamingId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Futures contracts to enable for bar data streaming.</summary>
    [Key(6)]
    public FuturesContractV2ReadModel[] Contracts { get; init; }

    /// <summary>The value (trading) date associated with the streaming session.</summary>
    [Key(7)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public StartFuturesBarDataStreamingCommand() { }

    /// <summary>
    /// Creates a new command to start futures bar data streaming.
    /// </summary>
    /// <param name="contracts">Array of futures contracts (cannot be null).</param>
    /// <param name="valueDate">Value date for the streaming session.</param>
    public StartFuturesBarDataStreamingCommand(FuturesContractV2ReadModel[] contracts, DateOnly valueDate)
    {
        Contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
        ValueDate = valueDate;

        EntityId = new FuturesBarDataStreamingId(ValueDate);
        RouteTo = BoundedContextName.FuturesBarDataBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters align with key order 0�7.
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Indicates whether to publish resulting events (key 2).</param>
    /// <param name="entityId">Futures bar data streaming entity identifier (key 3).</param>
    /// <param name="errorCode">Associated error code (key 4).</param>
    /// <param name="routeTo">Target bounded context (key 5).</param>
    /// <param name="contracts">The futures contracts payload (key 6).</param>
    /// <param name="valueDate">The value date (key 7).</param>
    [SerializationConstructor]
    public StartFuturesBarDataStreamingCommand(
        Guid commandId,                              // Key(0)
        ActorSubject subject,                        // Key(1)
        bool postEvents,                             // Key(2)
        FuturesBarDataStreamingId entityId,          // Key(3)
        int errorCode,                               // Key(4)
        BoundedContextName routeTo,                  // Key(5)
        FuturesContractV2ReadModel[] contracts,      // Key(6)
        DateOnly valueDate)                          // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Contracts = contracts;
        ValueDate = valueDate;
    }
}