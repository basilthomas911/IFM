using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Represents a command to generate an ITI (Intraday Trading Indicator) signal for a specified futures contract,
/// encapsulating all required parameters for signal generation and dispatch within a messaging infrastructure.
/// </summary>
/// <remarks>This command is typically used in distributed systems to request the generation of ITI signals based
/// on market data for futures contracts. It includes identifiers, value date, time period, timestamp, and relevant
/// price information. The command is designed for use with message-based workflows and supports serialization for
/// transport between services.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record GenerateFuturesItiSignalCommand : ICommand<FuturesItiSignalEntityId>
{
    public const string Actor = "FuturesItiSignalCommand";
    public const string Verb = "GenerateFuturesItiSignal";
    public const int ErrorId = 20011;

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

    /// <summary>The futures contract identifier (symbol root + month/year code).</summary>
    [Key(6)]
    public string ContractId { get; init; }

    /// <summary>The value date for which the signal is generated.</summary>
    [Key(7)]
    public DateOnly ValueDate { get; init; }

    [Key(8)]
    public TradeTimePeriodType TimePeriod { get; init; }    

    /// <summary>The timestamp of the source data used to generate the signal.</summary>
    [Key(9)]
    public DateTime Timestamp { get; init; }

    /// <summary>The latest futures price.</summary>
    [Key(10)]
    public double FuturesPrice { get; init; }

    [Key(11)]
    public double VixFuturesPrice { get; init; }

    /// <summary>Parameterless constructor for MessagePack deserialization.</summary>
    public GenerateFuturesItiSignalCommand() { }

    /// <summary>
    /// Creates a command to generate an ITI signal with the specified parameters.
    /// </summary>
    public GenerateFuturesItiSignalCommand(
        string contractId,
        DateOnly valueDate,
        TradeTimePeriodType timePeriod,
        DateTime timestamp,
        double futuresPrice,
        double vixFuturesPrice)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        Timestamp = timestamp;
        FuturesPrice = futuresPrice;
        VixFuturesPrice = vixFuturesPrice;

        EntityId = new(ContractId, ValueDate, TimePeriod);
        ErrorCode = 20011;
    }

    // Optional explicit serialization constructor (keys must match property indices)
    [SerializationConstructor]
    public GenerateFuturesItiSignalCommand(
        Guid commandId,                              // Key(0)
        ActorSubject subject,                        // Key(1)
        bool postEvents,                             // Key(2)
        FuturesItiSignalEntityId entityId,           // Key(3)
        int errorCode,                               // Key(4)
        BoundedContextName routeTo,                  // Key(5)
        string contractId,                           // Key(6)
        DateOnly valueDate,                          // Key(7)
        TradeTimePeriodType timePeriod,              // Key(8)
        DateTime timestamp,                          // Key(9)
        double futuresPrice,                         // Key(10)
        double vixFuturesPrice                       // Key(11)
    )
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
        FuturesPrice = futuresPrice;
        VixFuturesPrice = vixFuturesPrice;
    }
}