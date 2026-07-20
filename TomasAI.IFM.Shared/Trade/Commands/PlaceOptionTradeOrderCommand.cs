using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to place an option trade order with the associated trade data payload.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern where base command members use keys 0�5 and custom members start at key 6.
/// Routes to <see cref="BoundedContextName.OptionTradeBoundedContext"/> with error code 4007.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record PlaceOptionTradeOrderCommand : ICommand<OptionTradeEntityId>
{
    public const string Actor = "OptionTradeCommand";
    public const string Verb = "PlaceOrder";

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

    /// <summary>The trade order payload to be placed.</summary>
    [Key(6)]
    public TradeOrderReadModel TradeOrder { get; init; }

    /// <summary>The option trade data payload associated with the order.</summary>
    [Key(7)]
    public OptionTradeReadModel OptionTrade { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public PlaceOptionTradeOrderCommand() { }

    /// <summary>
    /// Creates a new command to place an option trade order.
    /// </summary>
    /// <param name="tradeOrder">The trade order view model (cannot be null).</param>
    /// <param name="optionTrade">The option trade data model (cannot be null).</param>
    public PlaceOptionTradeOrderCommand(TradeOrderReadModel tradeOrder, OptionTradeReadModel optionTrade)
    {
        TradeOrder = tradeOrder ?? throw new ArgumentNullException(nameof(tradeOrder));
        OptionTrade = optionTrade ?? throw new ArgumentNullException(nameof(optionTrade));

        EntityId = new OptionTradeEntityId(OptionTrade.OrderId, OptionTrade.TradeId);
        RouteTo = BoundedContextName.OptionTradeBoundedContext;
        ErrorCode = 4007;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public PlaceOptionTradeOrderCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        OptionTradeEntityId entityId,   // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        TradeOrderReadModel tradeOrder, // Key(6)
        OptionTradeReadModel optionTrade) // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        TradeOrder = tradeOrder;
        OptionTrade = optionTrade;
    }
}