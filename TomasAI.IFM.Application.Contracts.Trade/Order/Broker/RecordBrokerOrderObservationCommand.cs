using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

[MessagePackObject(AllowPrivate = true)]
public sealed record RecordBrokerOrderObservationCommand : ICommand<BrokerOrderId>
{

    /// <summary>Creates an empty command for serialization and existing callers.</summary>
    public RecordBrokerOrderObservationCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="observation">The Observation field.</param>
    [SerializationConstructor]
    public RecordBrokerOrderObservationCommand(Guid commandId, ActorSubject subject, bool postEvents, BrokerOrderId entityId, BrokerOrderObservationEvidence observation)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        Observation = observation;
    }
    public const string Actor = BrokerOrderActorNames.Command;
    public const string Verb = "RecordBrokerOrderObservation";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public BrokerOrderId EntityId { get; init; }
    [Key(4)] public BrokerOrderObservationEvidence Observation { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(RecordBrokerOrderObservationCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.BrokerOrderBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public int ErrorCode => 25103;
}
