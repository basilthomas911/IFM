using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

/// <summary>Requests broker evidence for one existing order identity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ReconcileBrokerOrderCommand : ICommand<BrokerOrderId>
{
    public ReconcileBrokerOrderCommand() { }

    [SerializationConstructor]
    public ReconcileBrokerOrderCommand(Guid commandId, ActorSubject subject, bool postEvents, BrokerOrderId entityId)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
    }

    public const string Actor = BrokerOrderActorNames.Command;
    public const string Verb = "ReconcileBrokerOrder";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public BrokerOrderId EntityId { get; init; }
    [IgnoreMember] public string CommandName => nameof(ReconcileBrokerOrderCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.BrokerOrderBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{BrokerOrderActorNames.Command}Actor";
    [IgnoreMember] public int ErrorCode => 25103;
}
