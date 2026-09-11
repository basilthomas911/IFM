using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public readonly record struct ParameterAssignmentEntityId([property:Key(0)] Guid AssignmentId):IActorEntityId
{public string Format()=>AssignmentId.ToString("N");}
public interface IParameterAssignmentMutation:ICommand<ParameterAssignmentEntityId>
{long ExpectedRevision{get;} ParameterAssignmentScope Scope{get;} ParameterVersionRef Reference{get;}}

[MessagePackObject]
public sealed record AssignParameterVersionCommand:IParameterAssignmentMutation
{
 public const string Actor="ParameterAssignmentCommand";public const string Verb="AssignParameterVersion";
 [Key(0)] public Guid CommandId{get;init;}
 [Key(1)] public ActorSubject Subject{get;init;}
 [Key(2)] public bool PostEvents{get;init;}=true;
 [Key(3)] public ParameterAssignmentEntityId EntityId{get;init;}
 [Key(4)] public int ErrorCode{get;init;}=33010;
 [Key(5)] public BoundedContextName RouteTo{get;init;}=BoundedContextName.StrategyConfigurationBoundedContext;
 [Key(6)] public long ExpectedRevision{get;init;}
 [Key(7)] public ParameterAssignmentScope Scope{get;init;}=null!;
 [Key(8)] public ParameterVersionRef Reference{get;init;}=null!;
 [IgnoreMember] public string CommandName=>nameof(AssignParameterVersionCommand);
 [IgnoreMember] public string StreamId=>Subject.StreamId;
 [IgnoreMember] public string EventSource=>Actor;
 [IgnoreMember] public DateTime OriginatedOn=>DateTime.UtcNow;
 [IgnoreMember] public string OriginatedBy=>$"{Environment.UserDomainName}\\{Environment.UserName}";
}

[MessagePackObject]
public sealed record DisableParameterAssignmentCommand:IParameterAssignmentMutation
{
 public const string Actor="ParameterAssignmentCommand";public const string Verb="DisableParameterAssignment";
 [Key(0)] public Guid CommandId{get;init;}
 [Key(1)] public ActorSubject Subject{get;init;}
 [Key(2)] public bool PostEvents{get;init;}=true;
 [Key(3)] public ParameterAssignmentEntityId EntityId{get;init;}
 [Key(4)] public int ErrorCode{get;init;}=33010;
 [Key(5)] public BoundedContextName RouteTo{get;init;}=BoundedContextName.StrategyConfigurationBoundedContext;
 [Key(6)] public long ExpectedRevision{get;init;}
 [Key(7)] public ParameterAssignmentScope Scope{get;init;}=null!;
 [Key(8)] public ParameterVersionRef Reference{get;init;}=null!;
 [IgnoreMember] public string CommandName=>nameof(DisableParameterAssignmentCommand);
 [IgnoreMember] public string StreamId=>Subject.StreamId;
 [IgnoreMember] public string EventSource=>Actor;
 [IgnoreMember] public DateTime OriginatedOn=>DateTime.UtcNow;
 [IgnoreMember] public string OriginatedBy=>$"{Environment.UserDomainName}\\{Environment.UserName}";
}
