using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to stop streaming futures tick data for a specific contract and value date.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern (base command keys 0–5). Custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.FuturesTickDataBoundedContext"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StopFuturesTickDataStreamingCommand : ICommand<FuturesDataId>
{
    public const string Actor = "FuturesTickDataCommand";
    public const string Verb = "StopStreaming";
    public const int ErrorId = 5005;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesDataId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The futures contract identifier whose tick data streaming should be stopped.</summary>
    [Key(6)]
    public string ContractId { get; init; }

    /// <summary>The value (trading) date associated with the tick data stream.</summary>
    [Key(7)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public StopFuturesTickDataStreamingCommand() { }

    /// <summary>
    /// Creates a new command to stop streaming futures tick data.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">The value (trading) date.</param>
    public StopFuturesTickDataStreamingCommand(string contractId, DateOnly valueDate)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;

        EntityId = new FuturesDataId(ContractId, ValueDate);
        RouteTo = BoundedContextName.FuturesTickDataBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters align with key order 0–7.
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Indicates whether to publish resulting events (key 2).</param>
    /// <param name="entityId">Futures data entity identifier (key 3).</param>
    /// <param name="errorCode">Associated error code (key 4).</param>
    /// <param name="routeTo">Target bounded context (key 5).</param>
    /// <param name="contractId">The futures contract identifier (key 6).</param>
    /// <param name="valueDate">The value date (key 7).</param>
    [SerializationConstructor]
    public StopFuturesTickDataStreamingCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        FuturesDataId entityId,         // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        string contractId,              // Key(6)
        DateOnly valueDate)             // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ContractId = contractId;
        ValueDate = valueDate;
    }
}