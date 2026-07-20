using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to clear (reset/remove) the forward loss limit for a specific trade plan.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.TradePlanForwardLossLimitBoundedContext"/> with error code 4035.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ClearTradePlanForwardLossLimitCommand
    : ICommand<TradePlanForwardLossLimitEntityId>
{
    public const string Actor = "TradePlanForwardLossLimitCommand";
    public const string Verb = "Clear";
    public const int ErrorId = 4035;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public TradePlanForwardLossLimitEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The identifier of the trade plan forward loss limit to clear.</summary>
    [Key(6)]
    public TradePlanForwardLossLimitEntityId TradePlanForwardLossLimitId { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ClearTradePlanForwardLossLimitCommand() { }

    /// <summary>
    /// Creates a new command to clear the forward loss limit for the specified trade plan.
    /// </summary>
    /// <param name="tradePlanForwardLossLimitId">The target trade plan forward loss limit identifier.</param>
    public ClearTradePlanForwardLossLimitCommand(TradePlanForwardLossLimitEntityId tradePlanForwardLossLimitId)
    {
        TradePlanForwardLossLimitId = tradePlanForwardLossLimitId;

        EntityId = TradePlanForwardLossLimitId;
        RouteTo = BoundedContextName.TradePlanForwardLossLimitBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public ClearTradePlanForwardLossLimitCommand(
        Guid commandId,                               // Key(0)
        ActorSubject subject,                         // Key(1)
        bool postEvents,                              // Key(2)
        TradePlanForwardLossLimitEntityId entityId,   // Key(3)
        int errorCode,                                // Key(4)
        BoundedContextName routeTo,                   // Key(5)
        TradePlanForwardLossLimitEntityId tradePlanForwardLossLimitId) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        TradePlanForwardLossLimitId = tradePlanForwardLossLimitId;
    }
}