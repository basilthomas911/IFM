using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public sealed record ParameterAssignmentChangedEvent:IEvent<ParameterAssignmentEntityId>
{
    public const string Actor="ParameterAssignment"; public const string Verb="ParameterAssignmentChanged";
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public Guid Id {get;init;}
    [Key(2)] public ParameterAssignmentEntityId EntityId {get;init;}
    [Key(3)] public long EventId {get;init;}
    [Key(4)] public Guid CommandId {get;init;}
    [Key(5)] public string AggregateId {get;init;}=string.Empty;
    [Key(6)] public string EventSource {get;init;}=string.Empty;
    [Key(7)] public DateTime ReceivedOn {get;init;}
    [Key(8)] public long Revision {get;init;}
    [Key(9)] public string RequestHash {get;init;}=string.Empty;
    [Key(10)] public string AssignmentJson {get;init;}=string.Empty;
    [Key(11)] public string AuditJson {get;init;}=string.Empty;
    [IgnoreMember] public string UserName=>string.Empty;
    [IgnoreMember] public string EventName=>nameof(ParameterAssignmentChangedEvent);
    [IgnoreMember] public EventType EventType=>EventType.DomainEvent;
}
