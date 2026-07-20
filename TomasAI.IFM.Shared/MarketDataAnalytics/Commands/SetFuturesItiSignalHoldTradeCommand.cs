using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Command to set the HoldTrade flag/state on a Futures ITI signal for a given contract and value date.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by analytics commands. The command is routed to
/// <see cref="BoundedContextName.FuturesItiSignalBoundedContext"/> and assigns base command fields in the constructor.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record SetFuturesItiSignalHoldTradeCommand : ICommand<FuturesItiSignalEntityId>
{
    public const string Actor = "FuturesItiSignalCommand";
    public const string Verb = "SetHoldTrade";
    public const int ErrorId = 20013;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesItiSignalEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(6)] public string ContractId { get; init; }
    [Key(7)] public DateOnly ValueDate { get; init; }
    [Key(8)] public TradeTimePeriodType TimePeriod { get; init; }
    [Key(9)] public DateTime Timestamp { get; init; }

    /// <summary>
    /// Parameterless constructor for MessagePack deserialization.
    /// </summary>
    public SetFuturesItiSignalHoldTradeCommand() { }

    /// <summary>
    /// Creates a command to set HoldTrade for the specified contract and value date.
    /// </summary>
    /// <param name="contractId">The futures contract identifier.</param>
    /// <param name="valueDate">The value date of the signal.</param>
    /// <param name="timePeriod">The time period type for the signal.</param>
    /// <param name="timestamp">The action timestamp.</param>
    public SetFuturesItiSignalHoldTradeCommand(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, DateTime timestamp)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        Timestamp = timestamp;

        EntityId = new FuturesItiSignalEntityId(ContractId, ValueDate, TimePeriod);
        ErrorCode = 20013;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters must align with key order 0–9.
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Whether resulting events should be posted (key 2).</param>
    /// <param name="entityId">Signal entity identifier (key 3).</param>
    /// <param name="errorCode">Associated error code (key 4).</param>
    /// <param name="routeTo">Target bounded context (key 5).</param>
    /// <param name="contractId">Contract identifier (key 6).</param>
    /// <param name="valueDate">Value date (key 7).</param>
    /// <param name="timePeriod">Time period type (key 8).</param>
    /// <param name="timestamp">Timestamp (key 9).</param>
    [SerializationConstructor]
    public SetFuturesItiSignalHoldTradeCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FuturesItiSignalEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        string contractId,
        DateOnly valueDate,
        TradeTimePeriodType timePeriod,
        DateTime timestamp)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        Timestamp = timestamp;
    }
}