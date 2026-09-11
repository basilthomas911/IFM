using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
public interface IParameterSetFact : IEvent<ParameterSetEntityId>
{
    long Revision {get;} string RequestHash {get;} string VersionJson {get;} string AuditJson {get;}
}
[MessagePackObject]
public sealed record ParameterSetCreatedEvent:IParameterSetFact
{
    public const string Actor="ParameterSet"; public const string Verb="ParameterSetCreated";
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public Guid Id {get;init;}
    [Key(2)] public ParameterSetEntityId EntityId {get;init;}
    [Key(3)] public long EventId {get;init;}
    [Key(4)] public Guid CommandId {get;init;}
    [Key(5)] public string AggregateId {get;init;}=string.Empty;
    [Key(6)] public string EventSource {get;init;}=string.Empty;
    [Key(7)] public DateTime ReceivedOn {get;init;}
    [Key(8)] public long Revision {get;init;}
    [Key(9)] public string RequestHash {get;init;}=string.Empty;
    [Key(10)] public string VersionJson {get;init;}=string.Empty;
    [Key(11)] public string AuditJson {get;init;}=string.Empty;
    [IgnoreMember] public string UserName=>string.Empty;
    [IgnoreMember] public string EventName=>nameof(ParameterSetCreatedEvent);
    [IgnoreMember] public EventType EventType=>EventType.DomainEvent;
}
[MessagePackObject]
public sealed record ParameterDraftSavedEvent:IParameterSetFact
{
    public const string Actor="ParameterSet"; public const string Verb="ParameterDraftSaved";
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public Guid Id {get;init;}
    [Key(2)] public ParameterSetEntityId EntityId {get;init;}
    [Key(3)] public long EventId {get;init;}
    [Key(4)] public Guid CommandId {get;init;}
    [Key(5)] public string AggregateId {get;init;}=string.Empty;
    [Key(6)] public string EventSource {get;init;}=string.Empty;
    [Key(7)] public DateTime ReceivedOn {get;init;}
    [Key(8)] public long Revision {get;init;}
    [Key(9)] public string RequestHash {get;init;}=string.Empty;
    [Key(10)] public string VersionJson {get;init;}=string.Empty;
    [Key(11)] public string AuditJson {get;init;}=string.Empty;
    [IgnoreMember] public string UserName=>string.Empty;
    [IgnoreMember] public string EventName=>nameof(ParameterDraftSavedEvent);
    [IgnoreMember] public EventType EventType=>EventType.DomainEvent;
}
[MessagePackObject]
public sealed record ParameterSetRenamedEvent:IParameterSetFact
{
    public const string Actor="ParameterSet"; public const string Verb="ParameterSetRenamed";
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public Guid Id {get;init;}
    [Key(2)] public ParameterSetEntityId EntityId {get;init;}
    [Key(3)] public long EventId {get;init;}
    [Key(4)] public Guid CommandId {get;init;}
    [Key(5)] public string AggregateId {get;init;}=string.Empty;
    [Key(6)] public string EventSource {get;init;}=string.Empty;
    [Key(7)] public DateTime ReceivedOn {get;init;}
    [Key(8)] public long Revision {get;init;}
    [Key(9)] public string RequestHash {get;init;}=string.Empty;
    [Key(10)] public string VersionJson {get;init;}=string.Empty;
    [Key(11)] public string AuditJson {get;init;}=string.Empty;
    [IgnoreMember] public string UserName=>string.Empty;
    [IgnoreMember] public string EventName=>nameof(ParameterSetRenamedEvent);
    [IgnoreMember] public EventType EventType=>EventType.DomainEvent;
}
[MessagePackObject]
public sealed record ParameterVersionPublishedEvent:IParameterSetFact
{
    public const string Actor="ParameterSet"; public const string Verb="ParameterVersionPublished";
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public Guid Id {get;init;}
    [Key(2)] public ParameterSetEntityId EntityId {get;init;}
    [Key(3)] public long EventId {get;init;}
    [Key(4)] public Guid CommandId {get;init;}
    [Key(5)] public string AggregateId {get;init;}=string.Empty;
    [Key(6)] public string EventSource {get;init;}=string.Empty;
    [Key(7)] public DateTime ReceivedOn {get;init;}
    [Key(8)] public long Revision {get;init;}
    [Key(9)] public string RequestHash {get;init;}=string.Empty;
    [Key(10)] public string VersionJson {get;init;}=string.Empty;
    [Key(11)] public string AuditJson {get;init;}=string.Empty;
    [IgnoreMember] public string UserName=>string.Empty;
    [IgnoreMember] public string EventName=>nameof(ParameterVersionPublishedEvent);
    [IgnoreMember] public EventType EventType=>EventType.DomainEvent;
}
[MessagePackObject]
public sealed record ParameterVersionRetiredEvent:IParameterSetFact
{
    public const string Actor="ParameterSet"; public const string Verb="ParameterVersionRetired";
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public Guid Id {get;init;}
    [Key(2)] public ParameterSetEntityId EntityId {get;init;}
    [Key(3)] public long EventId {get;init;}
    [Key(4)] public Guid CommandId {get;init;}
    [Key(5)] public string AggregateId {get;init;}=string.Empty;
    [Key(6)] public string EventSource {get;init;}=string.Empty;
    [Key(7)] public DateTime ReceivedOn {get;init;}
    [Key(8)] public long Revision {get;init;}
    [Key(9)] public string RequestHash {get;init;}=string.Empty;
    [Key(10)] public string VersionJson {get;init;}=string.Empty;
    [Key(11)] public string AuditJson {get;init;}=string.Empty;
    [IgnoreMember] public string UserName=>string.Empty;
    [IgnoreMember] public string EventName=>nameof(ParameterVersionRetiredEvent);
    [IgnoreMember] public EventType EventType=>EventType.DomainEvent;
}
