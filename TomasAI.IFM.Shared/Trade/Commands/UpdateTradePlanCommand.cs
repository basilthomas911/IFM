using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to update an existing trade plan using the provided <see cref="TradePlanReadModel"/> payload.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom members start at key 6.
/// Routes to <see cref="BoundedContextName.TradePlanBoundedContext"/> with error code 4016.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record UpdateTradePlanCommand
    : ICommand<TradePlanEntityId>
{
    public const string Actor = "TradePlanCommand";
    public const string Verb = "Update";
    public const int ErrorId = 4016;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public TradePlanEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The trade plan payload containing changes to apply.</summary>
    [Key(6)]
    public TradePlanReadModel TradePlan { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public UpdateTradePlanCommand() { }

    /// <summary>
    /// Creates a new command to update a trade plan.
    /// </summary>
    /// <param name="tradePlan">The trade plan view model (cannot be null).</param>
    public UpdateTradePlanCommand(TradePlanReadModel tradePlan)
    {
        TradePlan = tradePlan;

        EntityId = new TradePlanEntityId(TradePlan.OrderId, TradePlan.TradeId, TradePlan.ValueDate);
        RouteTo = BoundedContextName.TradePlanBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> assignments).
    /// </summary>
    [SerializationConstructor]
    public UpdateTradePlanCommand(
        Guid commandId,                  // Key(0)
        ActorSubject subject,            // Key(1)
        bool postEvents,                 // Key(2)
        TradePlanEntityId entityId,      // Key(3)
        int errorCode,                   // Key(4)
        BoundedContextName routeTo,      // Key(5)
        TradePlanReadModel tradePlan)    // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        TradePlan = tradePlan;
    }
}