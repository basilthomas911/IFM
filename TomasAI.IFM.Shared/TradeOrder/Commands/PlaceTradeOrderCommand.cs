using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Shared.TradeOrder.Commands;

/// <summary>
/// Command to place a trade order.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to TradeOrder bounded context with error code 5001.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record PlaceTradeOrderCommand : ICommand<TradeOrderEntityId>
{
    public const string Actor = "TradeOrderCommand";
    public const string Verb = "Place";

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

    /// <summary>The trade order payload to be placed.</summary>
    [Key(6)]
    public TradeOrderReadModel TradeOrder { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public PlaceTradeOrderCommand() { }

    /// <summary>
    /// Creates a new command to place a trade order.
    /// </summary>
    /// <param name="tradeOrder">Trade order view model (cannot be null).</param>
    public PlaceTradeOrderCommand(TradeOrderReadModel tradeOrder)
    {
        TradeOrder = tradeOrder ?? throw new ArgumentNullException(nameof(tradeOrder));

        EntityId = TradeOrder.EntityId;
        RouteTo = BoundedContextName.TradeOrderBoundedContext;
        ErrorCode = 5001;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public PlaceTradeOrderCommand(
        Guid commandId,                // Key(0)
        ActorSubject subject,          // Key(1)
        bool postEvents,               // Key(2)
        TradeOrderEntityId entityId,   // Key(3)
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