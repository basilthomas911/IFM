using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to close an option trade based on the provided trade order details.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom members start at key 6.
/// Routes to <see cref="BoundedContextName.OptionTradeBoundedContext"/> with error code 4006.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record CloseOptionTradeCommand : ICommand<OptionTradeEntityId>
{
    public const string Actor = "OptionTradeCommand";
    public const string Verb = "Close";

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

    /// <summary>The trade order payload used to close the option trade.</summary>
    [Key(6)]
    public TradeOrderReadModel TradeOrder { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack.
    /// </summary>
    public CloseOptionTradeCommand() { }

    /// <summary>
    /// Creates a new command to close an option trade.
    /// </summary>
    /// <param name="tradeOrder">The trade order payload (cannot be null).</param>
    public CloseOptionTradeCommand(TradeOrderReadModel tradeOrder)
    {
        TradeOrder = tradeOrder ?? throw new ArgumentNullException(nameof(tradeOrder));

        EntityId = new OptionTradeEntityId(TradeOrder.OrderId, TradeOrder.TradeId);
        RouteTo = BoundedContextName.OptionTradeBoundedContext;
        ErrorCode = 4006;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match Key attributes).
    /// </summary>
    [SerializationConstructor]
    public CloseOptionTradeCommand(
        Guid commandId,                // Key(0)
        ActorSubject subject,          // Key(1)
        bool postEvents,               // Key(2)
        OptionTradeEntityId entityId,  // Key(3)
        int errorCode,                 // Key(4)
        BoundedContextName routeTo,    // Key(5)
        TradeOrderReadModel tradeOrder // Key(6)
    )
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        TradeOrder = tradeOrder;
    }
}