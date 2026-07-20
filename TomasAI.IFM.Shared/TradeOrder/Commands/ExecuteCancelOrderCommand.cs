using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.TradeOrder.Commands;

/// <summary>
/// Command to execute the cancellation of a trade order.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.OptionTradeBoundedContext"/> with error code 5010.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ExecuteCancelOrderCommand
    : ICommand<TradeOrderEntityId>
{
    public const string Actor = "TradeOrderCommand";
    public const string Verb = "ExecuteCancel";
    public const int ErrorId = 5010;

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

    /// <summary>The trade order identifier to cancel.</summary>
    [Key(6)]
    public TradeOrderEntityId TradeOrderId { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ExecuteCancelOrderCommand() { }

    /// <summary>
    /// Creates a new command to execute a trade order cancellation.
    /// </summary>
    /// <param name="tradeOrderId">The trade order identifier.</param>
    public ExecuteCancelOrderCommand(TradeOrderEntityId tradeOrderId)
    {
        TradeOrderId = tradeOrderId;

        EntityId = TradeOrderId;
        RouteTo = BoundedContextName.OptionTradeBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public ExecuteCancelOrderCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        TradeOrderEntityId entityId,    // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        TradeOrderEntityId tradeOrderId // Key(6)
    )
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        TradeOrderId = tradeOrderId;
    }
}