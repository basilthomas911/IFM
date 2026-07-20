using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to change/update an option trade leg's calculated data for a specific trade context and value date.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.OptionTradeBoundedContext"/> with error code 4002.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ChangeOptionTradeLegDataCommand : ICommand<OptionTradeEntityId>
{
    public const string Actor = "OptionTradeCommand";
    public const string Verb = "ChangeLegData";

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

    /// <summary>Order identifier for the option trade.</summary>
    [Key(6)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(7)]
    public int TradeId { get; init; }

    /// <summary>Type of the option trade (strategy classification).</summary>
    [Key(8)]
    public TradeType TradeType { get; init; }

    /// <summary>Value (trading) date associated with the leg data.</summary>
    [Key(9)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Status of the trade at the time the leg data was calculated.</summary>
    [Key(10)]
    public TradeStatus TradeStatus { get; init; }

    /// <summary>Underlying asset price used for option calculations.</summary>
    [Key(11)]
    public decimal AssetPrice { get; init; }

    /// <summary>Risk-free interest rate used in option pricing.</summary>
    [Key(12)]
    public double RiskFreeRate { get; init; }

    /// <summary>Calculated option leg data payload.</summary>
    [Key(13)]
    public OptionTradeLegDataReadModel OptionLegData { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ChangeOptionTradeLegDataCommand() { }

    /// <summary>
    /// Creates a new command to update an option trade leg's data.
    /// </summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tradeId">Trade identifier.</param>
    /// <param name="tradeType">Option trade type.</param>
    /// <param name="valueDate">Value (trading) date.</param>
    /// <param name="tradeStatus">Trade status.</param>
    /// <param name="assetPrice">Underlying asset price.</param>
    /// <param name="riskFreeRate">Risk-free interest rate.</param>
    /// <param name="optionLegData">Option leg data view model (cannot be null).</param>
    public ChangeOptionTradeLegDataCommand(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        decimal assetPrice,
        double riskFreeRate,
        OptionTradeLegDataReadModel optionLegData)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        TradeStatus = tradeStatus;
        AssetPrice = assetPrice;
        RiskFreeRate = riskFreeRate;
        OptionLegData = optionLegData ?? throw new ArgumentNullException(nameof(optionLegData));

        EntityId = new OptionTradeEntityId(OrderId, TradeId);
        RouteTo = BoundedContextName.OptionTradeBoundedContext;
        ErrorCode = 4002;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public ChangeOptionTradeLegDataCommand(
        Guid commandId,                   // Key(0)
        ActorSubject subject,             // Key(1)
        bool postEvents,                  // Key(2)
        OptionTradeEntityId entityId,     // Key(3)
        int errorCode,                    // Key(4)
        BoundedContextName routeTo,       // Key(5)
        int orderId,                      // Key(6)
        int tradeId,                      // Key(7)
        TradeType tradeType,              // Key(8)
        DateOnly valueDate,               // Key(9)
        TradeStatus tradeStatus,          // Key(10)
        decimal assetPrice,               // Key(11)
        double riskFreeRate,              // Key(12)
        OptionTradeLegDataReadModel optionLegData) // Key(13)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;

        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        TradeStatus = tradeStatus;
        AssetPrice = assetPrice;
        RiskFreeRate = riskFreeRate;
        OptionLegData = optionLegData;
    }
}