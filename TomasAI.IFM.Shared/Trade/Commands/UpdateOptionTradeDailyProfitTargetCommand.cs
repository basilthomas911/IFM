using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to update an option trade's daily profit target parameters (trading days window).
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom members start at key 6.
/// Routes to <see cref="BoundedContextName.OptionTradeBoundedContext"/> with error code 4014.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record UpdateOptionTradeDailyProfitTargetCommand
    : ICommand<OptionTradeEntityId>
{
    public const string Actor = "OptionTradeCommand";
    public const string Verb = "UpdateDailyProfitTarget";
    public const int ErrorId = 4014;

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

    /// <summary>The parent order identifier.</summary>
    [Key(6)]
    public int OrderId { get; init; }

    /// <summary>The trade identifier within the order.</summary>
    [Key(7)]
    public int TradeId { get; init; }

    /// <summary>Number of trading days used for the daily profit target calculation.</summary>
    [Key(8)]
    public int TradingDays { get; init; }

    /// <summary>Maximum number of trading days allowed for the target window.</summary>
    [Key(9)]
    public int MaxTradingDays { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public UpdateOptionTradeDailyProfitTargetCommand() { }

    /// <summary>
    /// Creates a new command to update daily profit target parameters for an option trade.
    /// </summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tradeId">Trade identifier.</param>
    /// <param name="tradingDays">Trading days window.</param>
    /// <param name="maxTradingDays">Maximum trading days window.</param>
    public UpdateOptionTradeDailyProfitTargetCommand(int orderId, int tradeId, int tradingDays, int maxTradingDays)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradingDays = tradingDays;
        MaxTradingDays = maxTradingDays;

        EntityId = new OptionTradeEntityId(OrderId, TradeId);
        RouteTo = BoundedContextName.OptionTradeBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> assignments).
    /// </summary>
    [SerializationConstructor]
    public UpdateOptionTradeDailyProfitTargetCommand(
        Guid commandId,               // Key(0)
        ActorSubject subject,         // Key(1)
        bool postEvents,              // Key(2)
        OptionTradeEntityId entityId, // Key(3)
        int errorCode,                // Key(4)
        BoundedContextName routeTo,   // Key(5)
        int orderId,                  // Key(6)
        int tradeId,                  // Key(7)
        int tradingDays,              // Key(8)
        int maxTradingDays)           // Key(9)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;

        OrderId = orderId;
        TradeId = tradeId;
        TradingDays = tradingDays;
        MaxTradingDays = maxTradingDays;
    }
}