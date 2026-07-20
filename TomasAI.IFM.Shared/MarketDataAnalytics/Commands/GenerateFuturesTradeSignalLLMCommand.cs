using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Command to generate an LLM-enhanced futures trade signal using enriched end-of-day (EOD) market data
/// and a supplied price volatility metric.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used across analytics commands. The command routes to
/// <see cref="BoundedContextName.FuturesTradeSignalLLMBoundedContext"/>. Properties beyond the base command
/// occupy MessagePack keys starting at 6.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GenerateFuturesTradeSignalLLMCommand : ICommand<FuturesTradeSignalEntityId>
{
    public const string Actor = "FuturesTradeSignalLLMCommand";
    public const string Verb = "GenerateLLM";

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesTradeSignalEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// End-of-day futures data used as input to trade signal generation.
    /// </summary>
    [Key(6)] public FuturesEodDataV2ReadModel FuturesEodData { get; init; }
    
    [Key(7)] public TradeTimePeriodType TimePeriod { get; init; }

    /// <summary>
    /// External or model-derived price volatility factor influencing trade signal logic.
    /// </summary>
    [Key(8)] public double PriceVolatility { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public GenerateFuturesTradeSignalLLMCommand() { }

    /// <summary>
    /// Creates a new LLM trade signal generation command.
    /// </summary>
    /// <param name="futuresEodData">Enriched end-of-day futures data (cannot be null).</param>
    /// <param name="priceVolatility">Volatility metric applied in signal generation.</param>
    public GenerateFuturesTradeSignalLLMCommand(
        FuturesEodDataV2ReadModel futuresEodData,
        TradeTimePeriodType timePeriod,
        double priceVolatility)
    {
        FuturesEodData = futuresEodData ?? throw new ArgumentNullException(nameof(futuresEodData));
        PriceVolatility = priceVolatility;

        EntityId = new FuturesTradeSignalEntityId(FuturesEodData.ContractId ?? string.Empty, FuturesEodData.ValueDate, timePeriod);
        ErrorCode = 20004;
        RouteTo = BoundedContextName.FuturesTradeSignalLLMBoundedContext;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public GenerateFuturesTradeSignalLLMCommand(
        Guid commandId,                          // Key(0)
        ActorSubject subject,                    // Key(1)
        bool postEvents,                         // Key(2)
        FuturesTradeSignalEntityId entityId,     // Key(3)
        int errorCode,                           // Key(4)
        BoundedContextName routeTo,              // Key(5)
        FuturesEodDataV2ReadModel futuresEodData, // Key(6)
        TradeTimePeriodType timePeriod,           // Key(7)
        double priceVolatility)                  // Key(8)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FuturesEodData = futuresEodData;
        TimePeriod = timePeriod;
        PriceVolatility = priceVolatility;
    }
}