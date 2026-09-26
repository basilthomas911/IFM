using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using NewTradeOrderId = TomasAI.IFM.Domain.Trade.Shared.TradeOrderId;

namespace TomasAI.IFM.Domain.Trade.Shared.Order;

[MessagePackObject(AllowPrivate = true)]
public sealed record AmendTradeOrderCommand : ICommand<NewTradeOrderId>
{

    /// <summary>Creates an empty command for serialization and existing callers.</summary>
    public AmendTradeOrderCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="order">The Order field.</param>
    [SerializationConstructor]
    public AmendTradeOrderCommand(Guid commandId, ActorSubject subject, bool postEvents, NewTradeOrderId entityId, TradeOrderDefinition order)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        Order = order;
    }
    public const string Actor = TradeOrderActorNames.Command;
    public const string Verb = "AmendTradeOrder";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public NewTradeOrderId EntityId { get; init; }
    [Key(4)] public TradeOrderDefinition Order { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(AmendTradeOrderCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.TradeOrderBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public int ErrorCode => 25101;
}
