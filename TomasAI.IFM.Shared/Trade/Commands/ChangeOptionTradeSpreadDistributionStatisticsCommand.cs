using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to change an option trade's distribution statistics (put/call spread distributions) for a given trade context.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.OptionTradeBoundedContext"/> with error code 4002.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ChangeOptionTradeSpreadDistributionStatisticsCommand : ICommand<OptionTradeEntityId>
{
    public const string Actor = "OptionTradeCommand";
    public const string Verb = "ChangeSpreadDistributionStatistics";
    public const int ErrorId = 4002;

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
    [Key(6)] public int OrderId { get; init; }
    [Key(7)] public int TradeId { get; init; }
    [Key(8)] public double ForwardLossRatio { get; init; }
    [Key(9)] public double LossProbability { get; init; }
    [Key(10)] public DateOnly ValueDate { get; init; }
    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ChangeOptionTradeSpreadDistributionStatisticsCommand() { }

    /// <summary>
    /// Initializes a new instance of the ChangeOptionTradeSpreadDistributionStatisticsCommand class with the specified
    /// order and trade identifiers, forward loss ratio, loss probability, and value date.
    /// </summary>
    /// <remarks>This constructor sets the EntityId based on the provided order and trade identifiers and
    /// routes the command to the OptionTradeBoundedContext.</remarks>
    /// <param name="orderId">The unique identifier of the order associated with the option trade.</param>
    /// <param name="tradeId">The unique identifier of the option trade to be modified.</param>
    /// <param name="forwardLossRatio">The expected loss ratio in the forward direction, expressed as a decimal value.</param>
    /// <param name="lossProbability">The probability of incurring a loss on the trade, specified as a decimal value between 0 and 1.</param>
    /// <param name="valueDate">The date on which the trade's value is assessed.</param>
    public ChangeOptionTradeSpreadDistributionStatisticsCommand(
        int orderId,
        int tradeId,
        double forwardLossRatio,
        double lossProbability,
        DateOnly valueDate)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ForwardLossRatio = forwardLossRatio;
        LossProbability = lossProbability;
        ValueDate = valueDate;
        EntityId = new (OrderId, TradeId);
        RouteTo = BoundedContextName.OptionTradeBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> assignments).
    /// </summary>
    [SerializationConstructor]
    public ChangeOptionTradeSpreadDistributionStatisticsCommand(
        Guid commandId,                         // Key(0)
        ActorSubject subject,                   // Key(1)
        bool postEvents,                        // Key(2)
        OptionTradeEntityId entityId,           // Key(3)
        int errorCode,                          // Key(4)
        BoundedContextName routeTo,             // Key(5)
        int orderId,                            // Key(6)
        int tradeId,                            // Key(7)
       double forwardLossRatio,                    // Key(8)
        double lossProbability,                // Key(9)
        DateOnly valueDate) // Key(13)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;

        OrderId = orderId;
        TradeId = tradeId;
        ForwardLossRatio = forwardLossRatio;
        LossProbability = lossProbability;
        ValueDate = valueDate;
    }
}
