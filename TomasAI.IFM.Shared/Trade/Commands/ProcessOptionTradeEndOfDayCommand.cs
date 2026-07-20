using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to process end-of-day (EOD) metrics for an option trade (OHLCV and status) for a given value date.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom members start at key 6.
/// Routes to <see cref="BoundedContextName.OptionTradeBoundedContext"/> with error code 4008.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ProcessOptionTradeEndOfDayCommand : ICommand<OptionTradeEntityId>
{
    public const string Actor = "OptionTradeCommand";
    public const string Verb = "ProcessEndOfDay";

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public OptionTradeEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Fund identifier.</summary>
    [Key(6)]
    public int FundId { get; init; }

    /// <summary>Order identifier.</summary>
    [Key(7)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier.</summary>
    [Key(8)]
    public int TradeId { get; init; }

    /// <summary>Option trade strategy/type.</summary>
    [Key(9)]
    public TradeType TradeType { get; init; }

    /// <summary>Value (as-of) date for the EOD metrics.</summary>
    [Key(10)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Trade lifecycle status at EOD.</summary>
    [Key(11)]
    public TradeStatus TradeStatus { get; init; }

    /// <summary>Open price.</summary>
    [Key(12)]
    public decimal OpenPrice { get; init; }

    /// <summary>High price.</summary>
    [Key(13)]
    public decimal HighPrice { get; init; }

    /// <summary>Low price.</summary>
    [Key(14)]
    public decimal LowPrice { get; init; }

    /// <summary>Close price.</summary>
    [Key(15)]
    public decimal ClosePrice { get; init; }

    /// <summary>Trading volume.</summary>
    [Key(16)]
    public int Volume { get; init; }

    /// <summary>Optional reference or note.</summary>
    [Key(17)]
    public string Reference { get; init; } = string.Empty;

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ProcessOptionTradeEndOfDayCommand() { }

    /// <summary>
    /// Creates a new command to process end-of-day metrics for an option trade.
    /// </summary>
    public ProcessOptionTradeEndOfDayCommand(
        int fundId,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal closePrice,
        int volume,
        string reference)
    {
        FundId = fundId;
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        TradeStatus = tradeStatus;
        OpenPrice = openPrice;
        HighPrice = highPrice;
        LowPrice = lowPrice;
        ClosePrice = closePrice;
        Volume = volume;
        Reference = reference ?? string.Empty;

        EntityId = new OptionTradeEntityId(OrderId, TradeId);
        RouteTo = BoundedContextName.OptionTradeBoundedContext;
        ErrorCode = 4008;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public ProcessOptionTradeEndOfDayCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        OptionTradeEntityId entityId,   // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        int fundId,                     // Key(6)
        int orderId,                    // Key(7)
        int tradeId,                    // Key(8)
        TradeType tradeType,            // Key(9)
        DateOnly valueDate,             // Key(10)
        TradeStatus tradeStatus,        // Key(11)
        decimal openPrice,              // Key(12)
        decimal highPrice,              // Key(13)
        decimal lowPrice,               // Key(14)
        decimal closePrice,             // Key(15)
        int volume,                     // Key(16)
        string reference)               // Key(17)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;

        FundId = fundId;
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        TradeStatus = tradeStatus;
        OpenPrice = openPrice;
        HighPrice = highPrice;
        LowPrice = lowPrice;
        ClosePrice = closePrice;
        Volume = volume;
        Reference = reference;
    }
}