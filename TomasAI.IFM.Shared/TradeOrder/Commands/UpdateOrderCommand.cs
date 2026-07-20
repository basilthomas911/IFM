using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.TradeOrder.Commands;

/// <summary>
/// Command to update a trade order's price and execution status, with optional error details.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom properties start at key 6.
/// Routes to TradeOrder bounded context with error code 4005.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record UpdateOrderCommand
    : ICommand<TradeOrderEntityId>
{
    public const string Actor = "TradeOrderCommand";
    public const string Verb = "Update";
    public const int ErrorId = 4005;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public TradeOrderEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The trade order identifier to update.</summary>
    [Key(6)]
    public TradeOrderEntityId TradeOrderId { get; init; }

    /// <summary>The new order price to set.</summary>
    [Key(7)]
    public decimal OrderPrice { get; init; }

    /// <summary>Indicates whether the update has been executed.</summary>
    [Key(8)]
    public bool Executed { get; init; }

    /// <summary>Optional error message associated with the update operation.</summary>
    [Key(9)]
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public UpdateOrderCommand() { }

    /// <summary>
    /// Creates a new command to update a trade order.
    /// </summary>
    /// <param name="tradeOrderId">The trade order identifier.</param>
    /// <param name="orderPrice">The price to set on the order.</param>
    /// <param name="executed">True if the update has been executed.</param>
    /// <param name="errorMessage">Optional error message.</param>
    public UpdateOrderCommand(TradeOrderEntityId tradeOrderId, decimal orderPrice, bool executed, string errorMessage)
    {
        TradeOrderId = tradeOrderId;
        OrderPrice = orderPrice;
        Executed = executed;
        ErrorMessage = errorMessage ?? string.Empty;

        EntityId = TradeOrderId;
        RouteTo = BoundedContextName.TradeOrderBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public UpdateOrderCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        TradeOrderEntityId entityId,    // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        TradeOrderEntityId tradeOrderId,// Key(6)
        decimal orderPrice,             // Key(7)
        bool executed,                  // Key(8)
        string errorMessage)            // Key(9)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        TradeOrderId = tradeOrderId;
        OrderPrice = orderPrice;
        Executed = executed;
        ErrorMessage = errorMessage;
    }
}