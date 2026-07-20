using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Command to update a futures trade signal using the latest EOD data and optional RSI, TDI, and ITI analytics.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other analytics commands. Routes to
/// <see cref="BoundedContextName.FuturesTradeSignalLLMBoundedContext"/>.
/// Custom properties start at key index 6 because base command members occupy keys 0�5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record UpdateFuturesTradeSignalCommand
    : ICommand<FuturesTradeSignalEntityId>
{
    public const string Actor = "FuturesTradeSignalCommand";
    public const string Verb = "Update";
    public const int ErrorId = 20003;

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
    /// End-of-day futures data used as the core input for the trade signal update.
    /// </summary>
    [Key(6)]
    public FuturesEodDataV2ReadModel FuturesEodData { get; init; }

    /// <summary>
    /// Optional RSI signal metrics used to enrich the trade signal update.
    /// </summary>
    [Key(7)]
    public FuturesRsiSignalReadModel? FuturesRsiSignal { get; init; }

    /// <summary>
    /// Optional TDI (Trend/Divergence Index) signal metrics used to enrich the trade signal update.
    /// </summary>
    [Key(8)]
    public FuturesTdiSignalReadModel? FuturesTdiSignal { get; init; }

    /// <summary>
    /// Optional ITI signal data used to enrich the trade signal update.
    /// </summary>
    [Key(9)]
    public FuturesItiSignalDataReadModel? FuturesItiSignalData { get; init; }

    /// <summary>
    /// Optional VIX futures price used as an external volatility input.
    /// </summary>
    [Key(10)]
    public decimal VixFuturesPrice { get; init; }
    
    [Key(11)]
    public TradeTimePeriodType TimePeriod { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public UpdateFuturesTradeSignalCommand() { }

    /// <summary>
    /// Creates a new command to update a futures trade signal.
    /// </summary>
    /// <param name="futuresEodData">EOD futures data (cannot be null).</param>
    /// <param name="futuresRsiSignal">Optional RSI signal metrics.</param>
    /// <param name="futuresTdiSignal">Optional TDI signal metrics.</param>
    /// <param name="futuresItiSignalData">Optional ITI signal data.</param>
    /// <param name="vixFuturesPrice">Optional VIX futures price.</param>
    /// <param name="timePeriod">Time period for the trade signal.</param>
    public UpdateFuturesTradeSignalCommand(
        FuturesEodDataV2ReadModel futuresEodData,
        FuturesRsiSignalReadModel? futuresRsiSignal = null,
        FuturesTdiSignalReadModel? futuresTdiSignal = null,
        FuturesItiSignalDataReadModel? futuresItiSignalData = null,
        decimal vixFuturesPrice = 0,
        TradeTimePeriodType timePeriod = TradeTimePeriodType.FifteenSeconds )
    {
        FuturesEodData = futuresEodData ?? throw new ArgumentNullException(nameof(futuresEodData));
        FuturesRsiSignal = futuresRsiSignal;
        FuturesTdiSignal = futuresTdiSignal;
        FuturesItiSignalData = futuresItiSignalData;
        VixFuturesPrice = vixFuturesPrice;
        TimePeriod = timePeriod;

        EntityId = new FuturesTradeSignalEntityId(FuturesEodData.ContractId ?? string.Empty, FuturesEodData.ValueDate, timePeriod);
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FuturesTradeSignalLLMBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match property indices)
    [SerializationConstructor]
    public UpdateFuturesTradeSignalCommand(
        Guid commandId,                             // Key(0)
        ActorSubject subject,                       // Key(1)
        bool postEvents,                            // Key(2)
        FuturesTradeSignalEntityId entityId,              // Key(3)
        int errorCode,                              // Key(4)
        BoundedContextName routeTo,                 // Key(5)
        FuturesEodDataV2ReadModel futuresEodData,   // Key(6)
        FuturesRsiSignalReadModel? futuresRsiSignal,// Key(7)
        FuturesTdiSignalReadModel? futuresTdiSignal,// Key(8)
        FuturesItiSignalDataReadModel? futuresItiSignalData, // Key(9)
        decimal vixFuturesPrice, // Key(10)
        TradeTimePeriodType timePeriod // Key(11)
        )
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;

        FuturesEodData = futuresEodData;
        FuturesRsiSignal = futuresRsiSignal;
        FuturesTdiSignal = futuresTdiSignal;
        FuturesItiSignalData = futuresItiSignalData;
        VixFuturesPrice = vixFuturesPrice;
        TimePeriod = timePeriod;
    }
}