using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public sealed record ParameterStartupChangedEvent:IEvent<ParameterStartupEntityId>
{
    public const string Actor="ParameterStartup"; public const string Verb="ParameterStartupChanged";
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public Guid Id {get;init;}
    [Key(2)] public ParameterStartupEntityId EntityId {get;init;}
    [Key(3)] public long EventId {get;init;}
    [Key(4)] public Guid CommandId {get;init;}
    [Key(5)] public string AggregateId {get;init;}=string.Empty;
    [Key(6)] public string EventSource {get;init;}=string.Empty;
    [Key(7)] public DateTime ReceivedOn {get;init;}
    [Key(8)] public long Revision {get;init;}
    [Key(9)] public Guid RunId {get;init;}
    [Key(10)] public string RunJson {get;init;}=string.Empty;
    [Key(11)] public bool Released {get;init;}
    [Key(12)] public string ReportJson {get;init;}=string.Empty;
    [IgnoreMember] public string UserName=>string.Empty;
    [IgnoreMember] public string EventName=>nameof(ParameterStartupChangedEvent);
    [IgnoreMember] public EventType EventType=>EventType.DomainEvent;
}
