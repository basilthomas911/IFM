using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.TradeAlgorithm.Commands;

/// <summary>
/// Command to execute a Short Iron Condor trade algorithm for a specific option trade context and value date,
/// supplying supporting futures contract, end-of-day data, and trade signal inputs.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.TradeAlgorithmBoundedContext"/> with error code 7002.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ExecuteShortIronCondorAlgorithmCommand
    : ICommand<TradeAlgorithmId>
{
    public const string Actor = "TradeAlgorithmCommand";
    public const string Verb = "ExecuteShortIronCondor";
    public const int ErrorId = 7002;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public TradeAlgorithmId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Value (trading) date for the algorithm execution.</summary>
    [Key(6)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Trade type/classification (expected ShortIronCondor for this command context).</summary>
    [Key(7)]
    public TradeType TradeType { get; init; }

    /// <summary>Parent order identifier for the option trade.</summary>
    [Key(8)]
    public int OrderId { get; init; }

    /// <summary>Option trade identifier within the order.</summary>
    [Key(9)]
    public int TradeId { get; init; }

    /// <summary>Futures contract metadata supporting the algorithm (optional).</summary>
    [Key(10)]
    public FuturesContractV2ReadModel? FuturesContract { get; init; }

    /// <summary>Futures end-of-day data snapshot (optional).</summary>
    [Key(11)]
    public FuturesEodDataV2ReadModel? FuturesEodData { get; init; }

    /// <summary>Computed futures trade signal metrics (optional).</summary>
    [Key(12)]
    public FuturesTradeSignalV2ReadModel? FuturesTradeSignal { get; init; }

    /// <summary>
    /// Collection of option trades (legs/spreads) relevant to the algorithm (not serialized).
    /// </summary>
    [IgnoreMember]
    public IOptionTradeCollection? OptionTrades { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ExecuteShortIronCondorAlgorithmCommand() { }

    /// <summary>
    /// Creates a new command to execute the Short Iron Condor algorithm.
    /// </summary>
    /// <param name="valueDate">Trading date.</param>
    /// <param name="tradeType">Trade type (should be ShortIronCondor).</param>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tradeId">Trade identifier.</param>
    /// <param name="futuresContract">Associated futures contract (optional).</param>
    /// <param name="futuresEodData">Futures EOD data snapshot (optional).</param>
    /// <param name="futuresTradeSignal">Futures trade signal input (optional).</param>
    /// <param name="optionTrades">Option trades/legs collection (not serialized).</param>
    public ExecuteShortIronCondorAlgorithmCommand(
        DateOnly valueDate,
        TradeType tradeType,
        int orderId,
        int tradeId,
        FuturesContractV2ReadModel? futuresContract,
        FuturesEodDataV2ReadModel? futuresEodData,
        FuturesTradeSignalV2ReadModel? futuresTradeSignal,
        IOptionTradeCollection? optionTrades)
    {
        ValueDate = valueDate;
        TradeType = tradeType;
        OrderId = orderId;
        TradeId = tradeId;
        FuturesContract = futuresContract;
        FuturesEodData = futuresEodData;
        FuturesTradeSignal = futuresTradeSignal;
        OptionTrades = optionTrades;

        EntityId = new TradeAlgorithmId(ValueDate, OrderId, TradeId, TradeType);
        RouteTo = BoundedContextName.TradeAlgorithmBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public ExecuteShortIronCondorAlgorithmCommand(
        Guid commandId,                  // Key(0)
        ActorSubject subject,            // Key(1)
        bool postEvents,                 // Key(2)
        TradeAlgorithmId entityId,       // Key(3)
        int errorCode,                   // Key(4)
        BoundedContextName routeTo,      // Key(5)
        DateOnly valueDate,              // Key(6)
        TradeType tradeType,             // Key(7)
        int orderId,                     // Key(8)
        int tradeId,                     // Key(9)
        FuturesContractV2ReadModel? futuresContract,    // Key(10)
        FuturesEodDataV2ReadModel? futuresEodData,      // Key(11)
        FuturesTradeSignalV2ReadModel? futuresTradeSignal) // Key(12)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;

        ValueDate = valueDate;
        TradeType = tradeType;
        OrderId = orderId;
        TradeId = tradeId;
        FuturesContract = futuresContract;
        FuturesEodData = futuresEodData;
        FuturesTradeSignal = futuresTradeSignal;
        // OptionTrades intentionally excluded from serialization.
    }
}