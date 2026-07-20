using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to update the forward loss limit for a specific trade plan.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom members start at key 6.
/// Routes to <see cref="BoundedContextName.TradePlanForwardLossLimitBoundedContext"/> with error code 4034.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record UpdateTradePlanForwardLossLimitCommand
    : ICommand<TradePlanForwardLossLimitEntityId>
{
    public const string Actor = "TradePlanForwardLossLimitCommand";
    public const string Verb = "Update";
    public const int ErrorId = 4034;

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

    /// <summary>The forward loss limit payload to apply to the trade plan.</summary>
    [Key(6)]
    public TradePlanForwardLossLimitReadModel TradePlanForwardLossLimit { get; init; }

    /// <summary>Parameterless constructor required for MessagePack deserialization.</summary>
    public UpdateTradePlanForwardLossLimitCommand() { }

    /// <summary>
    /// Creates a new command to update the trade plan forward loss limit.
    /// </summary>
    /// <param name="tradePlanForwardLossLimit">The forward loss limit view model (cannot be null).</param>
    public UpdateTradePlanForwardLossLimitCommand(TradePlanForwardLossLimitReadModel tradePlanForwardLossLimit)
    {
        TradePlanForwardLossLimit = tradePlanForwardLossLimit ?? throw new ArgumentNullException(nameof(tradePlanForwardLossLimit));

        EntityId = TradePlanForwardLossLimit.EntityId;
        RouteTo = BoundedContextName.TradePlanForwardLossLimitBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> assignments).
    /// </summary>
    [SerializationConstructor]
    public UpdateTradePlanForwardLossLimitCommand(
        Guid commandId,                              // Key(0)
        ActorSubject subject,                        // Key(1)
        bool postEvents,                             // Key(2)
        TradePlanForwardLossLimitEntityId entityId,  // Key(3)
        int errorCode,                               // Key(4)
        BoundedContextName routeTo,                  // Key(5)
        TradePlanForwardLossLimitReadModel tradePlanForwardLossLimit) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        TradePlanForwardLossLimit = tradePlanForwardLossLimit;
    }
}