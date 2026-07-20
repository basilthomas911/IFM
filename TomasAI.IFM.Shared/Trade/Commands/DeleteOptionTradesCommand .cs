using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to delete all option trades associated with a specific order.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern: base command keys 0–5; custom members start at key 6.
/// Routes to <see cref="BoundedContextName.OptionTradeBoundedContext"/> with error code 4004.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record DeleteOptionTradesCommand
    : ICommand<OrderId>
{
    public const string Actor = "OptionTradeCommand";
    public const string Verb = "DeleteAll";
    public const int ErrorId = 4004;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public OrderId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The order identifier whose option trades should be deleted.</summary>
    [Key(6)]
    public OrderId OrderId { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public DeleteOptionTradesCommand() { }

    /// <summary>
    /// Creates a new command to delete all option trades for the specified order.
    /// </summary>
    /// <param name="orderId">The numeric order identifier.</param>
    public DeleteOptionTradesCommand(int orderId)
    {
        OrderId = new OrderId(orderId);

        EntityId = OrderId;
        RouteTo = BoundedContextName.OptionTradeBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> assignments).
    /// </summary>
    [SerializationConstructor]
    public DeleteOptionTradesCommand(
        Guid commandId,           // Key(0)
        ActorSubject subject,     // Key(1)
        bool postEvents,          // Key(2)
        OrderId entityId,         // Key(3)
        int errorCode,            // Key(4)
        BoundedContextName routeTo, // Key(5)
        OrderId orderId)          // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        OrderId = orderId;
    }
}