using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public sealed record ListParameterComponentsQuery:IQuery<ParameterComponentSummary[]>
{
 public const string Actor="ParameterSetQuery"; public const string Verb="ListParameterComponents";
 [Key(0)] public ActorSubject Subject {get;init;}
 [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
 [Key(2)] public Guid SetId {get;init;}
 [Key(3)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
 [Key(4)] public string PayloadJson {get;init;}="{}";
 [Key(5)] public int SchemaVersion {get;init;}=2;
 [IgnoreMember] public int ErrorCode {get;init;}=33101;
 [IgnoreMember] public string? QueryParams {get;init;}
}
[MessagePackObject]
public sealed record ListParameterVersionsQuery:IQuery<ParameterSetVersion[]>
{
 public const string Actor="ParameterSetQuery"; public const string Verb="ListParameterVersions";
 [Key(0)] public ActorSubject Subject {get;init;}
 [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
 [Key(2)] public Guid SetId {get;init;}
 [Key(3)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
 [Key(4)] public string PayloadJson {get;init;}="{}";
 [Key(5)] public int SchemaVersion {get;init;}=2;
 [Key(6)] public int Limit{get;init;}=100;
 [Key(7)] public string AfterName{get;init;}=string.Empty;
 [Key(8)] public Guid? AfterSetId{get;init;}
 [Key(9)] public int AfterVersion{get;init;}
 [IgnoreMember] public int ErrorCode {get;init;}=33101;
 [IgnoreMember] public string? QueryParams {get;init;}
}
[MessagePackObject]
public sealed record ValidateParameterCandidateQuery:IQuery<ParameterValidationReport>
{
 public const string Actor="ParameterSetQuery"; public const string Verb="ValidateParameterCandidate";
 [Key(0)] public ActorSubject Subject {get;init;}
 [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
 [Key(2)] public Guid SetId {get;init;}
 [Key(3)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
 [Key(4)] public string PayloadJson {get;init;}="{}";
 [Key(5)] public int SchemaVersion {get;init;}=2;
 [IgnoreMember] public int ErrorCode {get;init;}=33101;
 [IgnoreMember] public string? QueryParams {get;init;}
}
[MessagePackObject]
public sealed record CreateParameterDraftPreviewQuery:IQuery<string>
{
 public const string Actor="ParameterSetQuery"; public const string Verb="CreateParameterDraftPreview";
 [Key(0)] public ActorSubject Subject {get;init;}
 [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
 [Key(2)] public Guid SetId {get;init;}
 [Key(3)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
 [Key(4)] public string PayloadJson {get;init;}="{}";
 [Key(5)] public int SchemaVersion {get;init;}=ParameterSchemaRegistry.CurrentRegimeSchemaVersion;
 [Key(6)] public int TargetHorizon {get;init;}=(int)TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily;
 [Key(7)] public bool RebuildIntervals {get;init;}
 [IgnoreMember] public int ErrorCode {get;init;}=33101;
 [IgnoreMember] public string? QueryParams {get;init;}
}

[MessagePackObject]
public sealed record GetParameterSetStateQuery:IQuery<ParameterSetSnapshot>
{
 public const string Actor="ParameterSetQuery"; public const string Verb="GetParameterSetState";
 [Key(0)] public ActorSubject Subject {get;init;}
 [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
 [Key(2)] public Guid SetId {get;init;}
 [IgnoreMember] public int ErrorCode {get;init;}=33101;
 [IgnoreMember] public string? QueryParams {get;init;}
}

[MessagePackObject]
public sealed record GetParameterAssignmentQuery:IQuery<ParameterAssignmentSnapshot>
{
 public const string Actor="ParameterSetQuery"; public const string Verb="GetParameterAssignment";
 [Key(0)] public ActorSubject Subject{get;init;}
 [Key(1)] public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [Key(2)] public string WorkflowDefinitionId{get;init;}=string.Empty;
 [Key(3)] public int TargetHorizon{get;init;}
 [IgnoreMember] public int ErrorCode{get;init;}=33101;
 [IgnoreMember] public string? QueryParams{get;init;}
}

[MessagePackObject]
public sealed record GetParameterSchemaQuery:IQuery<ParameterSchemaDefinition>
{
 public const string Actor="ParameterSetQuery";public const string Verb="GetParameterSchema";
 [Key(0)]public ActorSubject Subject{get;init;}
 [Key(1)]public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [Key(2)]public string ComponentCode{get;init;}="strategy-workflow.regime-discovery";
 [Key(3)]public int SchemaVersion{get;init;}=ParameterSchemaRegistry.CurrentRegimeSchemaVersion;
 [IgnoreMember]public int ErrorCode{get;init;}=33101;
 [IgnoreMember]public string? QueryParams{get;init;}
}

[MessagePackObject]
public sealed record PreviewSignalStartupPlanQuery:IQuery<ParameterSignalStartupPlan>
{
 public const string Actor="ParameterSetQuery";public const string Verb="PreviewSignalStartupPlan";
 [Key(0)]public ActorSubject Subject{get;init;}
 [Key(1)]public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [Key(2)]public Guid StartupRunId{get;init;}
 [IgnoreMember]public int ErrorCode{get;init;}=33101;
 [IgnoreMember]public string? QueryParams{get;init;}
}

[MessagePackObject]
public sealed record GetParameterStartupRunsQuery:IQuery<ParameterStartupRun[]>
{
 public const string Actor="ParameterSetQuery";public const string Verb="GetParameterStartupRuns";
 [Key(0)]public ActorSubject Subject{get;init;}
 [Key(1)]public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [IgnoreMember]public int ErrorCode{get;init;}=33101;
 [IgnoreMember]public string? QueryParams{get;init;}
}

[MessagePackObject]
public sealed record GetParameterStartupReportQuery:IQuery<ParameterSignalStartupReport>
{
 public const string Actor="ParameterSetQuery";public const string Verb="GetParameterStartupReport";
 [Key(0)]public ActorSubject Subject{get;init;}
 [Key(1)]public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [Key(2)]public Guid RunId{get;init;}
 [IgnoreMember]public int ErrorCode{get;init;}=33101;
 [IgnoreMember]public string? QueryParams{get;init;}
}

[MessagePackObject]
public sealed record GetParameterSignalMonitoringQuery:IQuery<ParameterSignalMonitoringSnapshot>
{
 public const string Actor="ParameterSetQuery";public const string Verb="GetParameterSignalMonitoring";
 [Key(0)]public ActorSubject Subject{get;init;}
 [Key(1)]public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [Key(2)]public Guid RunId{get;init;}
 [IgnoreMember]public int ErrorCode{get;init;}=33101;
 [IgnoreMember]public string? QueryParams{get;init;}
}

[MessagePackObject]
public sealed record ListLegacyParameterVersionsQuery:IQuery<ParameterLegacyVersion[]>
{
 public const string Actor="ParameterSetQuery";public const string Verb="ListLegacyParameterVersions";
 [Key(0)]public ActorSubject Subject{get;init;}
 [Key(1)]public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [Key(2)]public int Offset{get;init;}
 [IgnoreMember]public int ErrorCode{get;init;}=33101;
 [IgnoreMember]public string? QueryParams{get;init;}
}


[MessagePackObject]
public sealed record PreviewLegacyParameterMigrationQuery:IQuery<CreateParameterSetCommand>
{
 public const string Actor="ParameterSetQuery";public const string Verb="PreviewLegacyParameterMigration";
 [Key(0)]public ActorSubject Subject{get;init;}
 [Key(1)]public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [Key(2)]public Guid SetId{get;init;}
 [Key(3)]public int Version{get;init;}
 [IgnoreMember]public int ErrorCode{get;init;}=33101;
 [IgnoreMember]public string? QueryParams{get;init;}
}

