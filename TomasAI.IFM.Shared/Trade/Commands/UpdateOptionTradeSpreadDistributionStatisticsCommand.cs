using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to update an option trade's distribution statistics (put/call spread distributions) for a given trade context.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.OptionTradeBoundedContext"/> with error code 4001.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record UpdateOptionTradeSpreadDistributionStatisticsCommand : ICommand<OptionTradeEntityId>
{
    public const string Actor = "OptionTradeCommand";
    public const string Verb = "UpdateSpreadDistributionStatistics";
    public const int ErrorId = 4001;

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

    /// <summary>Status of the trade at the time of distribution calculation.</summary>
    [Key(9)]
    public TradeStatus TradeStatus { get; init; }

    /// <summary>Value (trading) date associated with the distributions.</summary>
    [Key(10)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Remaining days to expiry for the underlying option position.</summary>
    [Key(11)]
    public int DaysToExpiry { get; init; }

    /// <summary>Put-side spread distribution snapshot.</summary>
    [Key(12)]
    public SpreadDistributionReadModel PutSpreadDistribution { get; init; }

    /// <summary>Call-side spread distribution snapshot.</summary>
    [Key(13)]
    public SpreadDistributionReadModel CallSpreadDistribution { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public UpdateOptionTradeSpreadDistributionStatisticsCommand() { }

    /// <summary>
    /// Creates a new command to change an option trade's distribution statistics.
    /// </summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tradeId">Trade identifier.</param>
    /// <param name="tradeType">Option trade type.</param>
    /// <param name="tradeStatus">Trade status.</param>
    /// <param name="valueDate">Value (trading) date.</param>
    /// <param name="daysToExpiry">Days remaining to expiry.</param>
    /// <param name="putSpreadDistribution">Put-side distribution (cannot be null).</param>
    /// <param name="callSpreadDistribution">Call-side distribution (cannot be null).</param>
    public UpdateOptionTradeSpreadDistributionStatisticsCommand(
        int orderId,
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry,
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        TradeStatus = tradeStatus;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        PutSpreadDistribution = putSpreadDistribution ?? throw new ArgumentNullException(nameof(putSpreadDistribution));
        CallSpreadDistribution = callSpreadDistribution ?? throw new ArgumentNullException(nameof(callSpreadDistribution));

        EntityId = new (OrderId, TradeId);
        RouteTo = BoundedContextName.OptionTradeBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> assignments).
    /// </summary>
    [SerializationConstructor]
    public UpdateOptionTradeSpreadDistributionStatisticsCommand(
        Guid commandId,                         // Key(0)
        ActorSubject subject,                   // Key(1)
        bool postEvents,                        // Key(2)
        OptionTradeEntityId entityId,           // Key(3)
        int errorCode,                          // Key(4)
        BoundedContextName routeTo,             // Key(5)
        int orderId,                            // Key(6)
        int tradeId,                            // Key(7)
        TradeType tradeType,                    // Key(8)
        TradeStatus tradeStatus,                // Key(9)
        DateOnly valueDate,                     // Key(10)
        int daysToExpiry,                       // Key(11)
        SpreadDistributionReadModel putSpreadDistribution,  // Key(12)
        SpreadDistributionReadModel callSpreadDistribution) // Key(13)
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
        TradeStatus = tradeStatus;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        PutSpreadDistribution = putSpreadDistribution;
        CallSpreadDistribution = callSpreadDistribution;
    }
}