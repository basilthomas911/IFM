using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

[MessagePackObject(AllowPrivate = true)]
public sealed record RequestBrokerOrderCancelCommand : ICommand<BrokerOrderId>
{

    /// <summary>Creates an empty command for serialization and existing callers.</summary>
    public RequestBrokerOrderCancelCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="operationId">The OperationId field.</param>
    /// <param name="effectiveAtUtc">The EffectiveAtUtc field.</param>
    [SerializationConstructor]
    public RequestBrokerOrderCancelCommand(Guid commandId, ActorSubject subject, bool postEvents, BrokerOrderId entityId, Guid operationId, DateTime effectiveAtUtc)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        OperationId = operationId;
        EffectiveAtUtc = effectiveAtUtc;
    }
    public const string Actor = BrokerOrderActorNames.Command;
    public const string Verb = "RequestBrokerOrderCancel";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public BrokerOrderId EntityId { get; init; }
    [Key(4)] public Guid OperationId { get; init; }
    [Key(5)] public DateTime EffectiveAtUtc { get; init; }
    [IgnoreMember] public string CommandName => nameof(RequestBrokerOrderCancelCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.BrokerOrderBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public int ErrorCode => 25103;
}
