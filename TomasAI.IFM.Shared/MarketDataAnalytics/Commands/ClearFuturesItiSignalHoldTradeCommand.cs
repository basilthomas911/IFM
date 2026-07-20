using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Represents a command to clear the hold-trade state for a specific futures ITI signal.
/// </summary>
/// <remarks>
/// MessagePack-serializable using numeric keys:
/// - Base command members use keys 0–5 (<see cref="BaseCommand{TEntityId}"/>).
/// - Payload members in this class use keys 6–8.
/// Routes to <see cref="BoundedContextName.FuturesItiSignalBoundedContext"/> and targets
/// <see cref="FuturesItiSignalEntityId"/> derived from <see cref="ContractId"/> and <see cref="ValueDate"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ClearFuturesItiSignalHoldTradeCommand : ICommand<FuturesItiSignalEntityId>
{
    public const string Actor = "FuturesItiSignalCommand";
    public const string Verb = "ClearHoldTrade";
    public const int ErrorId = 20012;

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

    [Key(6)] public string ContractId { get; init; } = string.Empty;
    [Key(7)] public DateOnly ValueDate { get; init; }
    [Key(8)] public TradeTimePeriodType TimePeriod { get; init; }
    [Key(9)] public DateTime Timestamp { get; init; }

    /// <summary>
    /// Parameterless constructor used by MessagePack for property-based deserialization.
    /// </summary>
    public ClearFuturesItiSignalHoldTradeCommand() { }

    /// <summary>
    /// Convenience constructor for application code.
    /// </summary>
    /// <param name="contractId">The futures contract identifier.</param>
    /// <param name="valueDate">The value date of the signal.</param>
    /// <param name="timePeriod">The time period type for the signal.</param>
    /// <param name="timestamp">The timestamp of the hold-trade state to clear.</param>
    public ClearFuturesItiSignalHoldTradeCommand(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, DateTime timestamp)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        Timestamp = timestamp;

        EntityId = new(ContractId, ValueDate, TimePeriod);
        ErrorCode = 20012;
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
    public ClearFuturesItiSignalHoldTradeCommand(
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
