using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public readonly record struct ParameterSetEntityId([property:Key(0)] Guid SetId):IActorEntityId
{ public string Format()=>SetId.ToString("N"); public override string ToString()=>Format(); }
public interface IParameterSetMutation : ICommand<ParameterSetEntityId>
{
    string OriginatedBy {get;} long ExpectedRevision {get;} int Version {get;} string ComponentCode {get;}
    string Name {get;} string Description {get;} int SchemaVersion {get;} string PayloadJson {get;}
}
[MessagePackObject]
public sealed record CreateParameterSetCommand:IParameterSetMutation
{
    public const string Actor="ParameterSetCommand";
    public const string Verb="CreateParameterSet";
    [Key(0)] public Guid CommandId {get;init;}
    [Key(1)] public ActorSubject Subject {get;init;}
    [Key(2)] public bool PostEvents {get;init;}=true;
    [Key(3)] public ParameterSetEntityId EntityId {get;init;}
    [Key(4)] public int ErrorCode {get;init;}=33010;
    [Key(5)] public BoundedContextName RouteTo {get;init;}=BoundedContextName.StrategyConfigurationBoundedContext;
    [Key(6)] public long ExpectedRevision {get;init;}
    [Key(7)] public int Version {get;init;}
    [Key(8)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
    [Key(9)] public string Name {get;init;}=string.Empty;
    [Key(10)] public string Description {get;init;}=string.Empty;
    [Key(11)] public int SchemaVersion {get;init;}=2;
    [Key(12)] public string PayloadJson {get;init;}="{}";
    [Key(13)] public ParameterLegacyReference? LegacySource {get;init;}
    [IgnoreMember] public string CommandName=>nameof(CreateParameterSetCommand);
    [IgnoreMember] public string StreamId=>Subject.StreamId;
    [IgnoreMember] public string EventSource=>Actor;
    [IgnoreMember] public DateTime OriginatedOn=>DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy=>$"{Environment.UserDomainName}\\{Environment.UserName}";
}
[MessagePackObject]
public sealed record SaveParameterDraftCommand:IParameterSetMutation
{
    public const string Actor="ParameterSetCommand";
    public const string Verb="SaveParameterDraft";
    [Key(0)] public Guid CommandId {get;init;}
    [Key(1)] public ActorSubject Subject {get;init;}
    [Key(2)] public bool PostEvents {get;init;}=true;
    [Key(3)] public ParameterSetEntityId EntityId {get;init;}
    [Key(4)] public int ErrorCode {get;init;}=33010;
    [Key(5)] public BoundedContextName RouteTo {get;init;}=BoundedContextName.StrategyConfigurationBoundedContext;
    [Key(6)] public long ExpectedRevision {get;init;}
    [Key(7)] public int Version {get;init;}
    [Key(8)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
    [Key(9)] public string Name {get;init;}=string.Empty;
    [Key(10)] public string Description {get;init;}=string.Empty;
    [Key(11)] public int SchemaVersion {get;init;}=2;
    [Key(12)] public string PayloadJson {get;init;}="{}";
    [IgnoreMember] public string CommandName=>nameof(SaveParameterDraftCommand);
    [IgnoreMember] public string StreamId=>Subject.StreamId;
    [IgnoreMember] public string EventSource=>Actor;
    [IgnoreMember] public DateTime OriginatedOn=>DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy=>$"{Environment.UserDomainName}\\{Environment.UserName}";
}
[MessagePackObject]
public sealed record RenameParameterSetCommand:IParameterSetMutation
{
    public const string Actor="ParameterSetCommand";
    public const string Verb="RenameParameterSet";
    [Key(0)] public Guid CommandId {get;init;}
    [Key(1)] public ActorSubject Subject {get;init;}
    [Key(2)] public bool PostEvents {get;init;}=true;
    [Key(3)] public ParameterSetEntityId EntityId {get;init;}
    [Key(4)] public int ErrorCode {get;init;}=33010;
    [Key(5)] public BoundedContextName RouteTo {get;init;}=BoundedContextName.StrategyConfigurationBoundedContext;
    [Key(6)] public long ExpectedRevision {get;init;}
    [Key(7)] public int Version {get;init;}
    [Key(8)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
    [Key(9)] public string Name {get;init;}=string.Empty;
    [Key(10)] public string Description {get;init;}=string.Empty;
    [Key(11)] public int SchemaVersion {get;init;}=2;
    [Key(12)] public string PayloadJson {get;init;}="{}";
    [IgnoreMember] public string CommandName=>nameof(RenameParameterSetCommand);
    [IgnoreMember] public string StreamId=>Subject.StreamId;
    [IgnoreMember] public string EventSource=>Actor;
    [IgnoreMember] public DateTime OriginatedOn=>DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy=>$"{Environment.UserDomainName}\\{Environment.UserName}";
}
[MessagePackObject]
public sealed record PublishParameterVersionCommand:IParameterSetMutation
{
    public const string Actor="ParameterSetCommand";
    public const string Verb="PublishParameterVersion";
    [Key(0)] public Guid CommandId {get;init;}
    [Key(1)] public ActorSubject Subject {get;init;}
    [Key(2)] public bool PostEvents {get;init;}=true;
    [Key(3)] public ParameterSetEntityId EntityId {get;init;}
    [Key(4)] public int ErrorCode {get;init;}=33010;
    [Key(5)] public BoundedContextName RouteTo {get;init;}=BoundedContextName.StrategyConfigurationBoundedContext;
    [Key(6)] public long ExpectedRevision {get;init;}
    [Key(7)] public int Version {get;init;}
    [Key(8)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
    [Key(9)] public string Name {get;init;}=string.Empty;
    [Key(10)] public string Description {get;init;}=string.Empty;
    [Key(11)] public int SchemaVersion {get;init;}=2;
    [Key(12)] public string PayloadJson {get;init;}="{}";
    [IgnoreMember] public string CommandName=>nameof(PublishParameterVersionCommand);
    [IgnoreMember] public string StreamId=>Subject.StreamId;
    [IgnoreMember] public string EventSource=>Actor;
    [IgnoreMember] public DateTime OriginatedOn=>DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy=>$"{Environment.UserDomainName}\\{Environment.UserName}";
}
[MessagePackObject]
public sealed record RetireParameterVersionCommand:IParameterSetMutation
{
    public const string Actor="ParameterSetCommand";
    public const string Verb="RetireParameterVersion";
    [Key(0)] public Guid CommandId {get;init;}
    [Key(1)] public ActorSubject Subject {get;init;}
    [Key(2)] public bool PostEvents {get;init;}=true;
    [Key(3)] public ParameterSetEntityId EntityId {get;init;}
    [Key(4)] public int ErrorCode {get;init;}=33010;
    [Key(5)] public BoundedContextName RouteTo {get;init;}=BoundedContextName.StrategyConfigurationBoundedContext;
    [Key(6)] public long ExpectedRevision {get;init;}
    [Key(7)] public int Version {get;init;}
    [Key(8)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
    [Key(9)] public string Name {get;init;}=string.Empty;
    [Key(10)] public string Description {get;init;}=string.Empty;
    [Key(11)] public int SchemaVersion {get;init;}=2;
    [Key(12)] public string PayloadJson {get;init;}="{}";
    [IgnoreMember] public string CommandName=>nameof(RetireParameterVersionCommand);
    [IgnoreMember] public string StreamId=>Subject.StreamId;
    [IgnoreMember] public string EventSource=>Actor;
    [IgnoreMember] public DateTime OriginatedOn=>DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy=>$"{Environment.UserDomainName}\\{Environment.UserName}";
}
