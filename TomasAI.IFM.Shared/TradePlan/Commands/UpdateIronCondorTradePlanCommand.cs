using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.TradePlan.Commands;

/// <summary>
/// Command to update an Iron Condor trade plan with latest option trades context, futures EOD data, M-score and fund balance.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.IronCondorTradeAlgorithmBoundedContext"/> with error code 4016.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record UpdateIronCondorTradePlanCommand
    : ICommand<IronCondorTradePlanId>
{
    public const string Actor = "IronCondorTradePlanCommand";
    public const string Verb = "Update";
    public const int ErrorId = 4016;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public IronCondorTradePlanId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The value (trading) date for the trade plan.</summary>
    [Key(6)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// The collection of option trades forming the Iron Condor strategy (not serialized).
    /// </summary>
    [IgnoreMember]
    public IOptionTradeCollection OptionTrades { get; init; }

    /// <summary>The futures end-of-day data relevant to this trade plan.</summary>
    [Key(7)]
    public FuturesEodDataV2ReadModel FuturesEodData { get; init; }

    /// <summary>M-score used to evaluate performance/risk.</summary>
    [Key(8)]
    public double MScore { get; init; }

    /// <summary>Current fund balance to consider in the trade plan.</summary>
    [Key(9)]
    public decimal FundBalance { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public UpdateIronCondorTradePlanCommand() { }

    /// <summary>
    /// Creates a new command to update an Iron Condor trade plan.
    /// </summary>
    /// <param name="valueDate">The trade plan valuation date.</param>
    /// <param name="optionTrades">The option trades defining the Iron Condor strategy.</param>
    /// <param name="futuresEodData">Futures end-of-day data relevant to the plan.</param>
    /// <param name="mScore">M-score for plan evaluation.</param>
    /// <param name="fundBalance">Current fund balance.</param>
    public UpdateIronCondorTradePlanCommand(
        DateOnly valueDate,
        IOptionTradeCollection optionTrades,
        FuturesEodDataV2ReadModel futuresEodData,
        double mScore,
        decimal fundBalance)
    {
        ValueDate = valueDate;
        OptionTrades = optionTrades ?? throw new ArgumentNullException(nameof(optionTrades));
        FuturesEodData = futuresEodData ?? throw new ArgumentNullException(nameof(futuresEodData));
        MScore = mScore;
        FundBalance = fundBalance;

        var primary = OptionTrades.PrimaryTrade ?? throw new ArgumentException("OptionTrades.PrimaryTrade cannot be null.", nameof(optionTrades));
        EntityId = IronCondorTradePlanId.Create(
            primary.OrderId,
            primary.TradeId,
            primary.TradeType,
            ValueDate,
            primary.TradeDate,
            primary.MaturityDate);

        RouteTo = BoundedContextName.IronCondorTradeAlgorithmBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// Note: OptionTrades is intentionally excluded from serialization.
    /// </summary>
    [SerializationConstructor]
    public UpdateIronCondorTradePlanCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        IronCondorTradePlanId entityId, // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        DateOnly valueDate,             // Key(6)
        FuturesEodDataV2ReadModel futuresEodData, // Key(7)
        double mScore,                  // Key(8)
        decimal fundBalance)            // Key(9)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;

        ValueDate = valueDate;
        FuturesEodData = futuresEodData;
        MScore = mScore;
        FundBalance = fundBalance;
    }
}