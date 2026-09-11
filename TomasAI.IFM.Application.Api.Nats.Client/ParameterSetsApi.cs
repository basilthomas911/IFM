using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.Api.Nats.Client;
public sealed class ParameterSetsApi(IActorProducer producer):NatsClientApi(producer),IParameterSetsApi
{
 public Task<ServiceResult<ParameterSignalStartupPlan>> PreviewStartupAsync(Guid runId,CancellationToken token=default){var q=new PreviewSignalStartupPlanQuery{StartupRunId=runId,Subject=QuerySubject(PreviewSignalStartupPlanQuery.Verb)};return RequestAsync<PreviewSignalStartupPlanQuery,ParameterSignalStartupPlan>(q.Subject,q,token).AsTask();}
 public Task<ServiceResult<ParameterStartupRun[]>> StartupRunsAsync(CancellationToken token=default){var q=new GetParameterStartupRunsQuery{Subject=QuerySubject(GetParameterStartupRunsQuery.Verb)};return RequestAsync<GetParameterStartupRunsQuery,ParameterStartupRun[]>(q.Subject,q,token).AsTask();}
 public Task<ServiceResult<GuidResult>> ApplyStartupAsync(ApplySignalStartupPlanCommand command,CancellationToken token=default)
 {var c=command with {EntityId=ParameterStartupEntityId.Registry,Subject=new ActorSubject(ActorType.Command,ApplySignalStartupPlanCommand.Actor,ApplySignalStartupPlanCommand.Verb,ParameterStartupEntityId.Registry.Format())};return RequestCommandResultAsync<ApplySignalStartupPlanCommand,ParameterStartupEntityId,GuidResult>(c,c.EntityId,token).AsTask();}
 public Task<ServiceResult<GuidResult>> ReleaseStartupAsync(ReleaseSignalStartupPlanCommand command,CancellationToken token=default)
 {var c=command with {EntityId=ParameterStartupEntityId.Registry,Subject=new ActorSubject(ActorType.Command,ReleaseSignalStartupPlanCommand.Actor,ReleaseSignalStartupPlanCommand.Verb,ParameterStartupEntityId.Registry.Format())};return RequestCommandResultAsync<ReleaseSignalStartupPlanCommand,ParameterStartupEntityId,GuidResult>(c,c.EntityId,token).AsTask();}
 public Task<ServiceResult<GuidResult>> RecordStartupReportAsync(RecordSignalStartupReportCommand command,CancellationToken token=default)
 {var c=command with {EntityId=ParameterStartupEntityId.Registry,Subject=new ActorSubject(ActorType.Command,RecordSignalStartupReportCommand.Actor,RecordSignalStartupReportCommand.Verb,ParameterStartupEntityId.Registry.Format())};return RequestCommandResultAsync<RecordSignalStartupReportCommand,ParameterStartupEntityId,GuidResult>(c,c.EntityId,token).AsTask();}
 public Task<ServiceResult<ParameterSignalStartupReport>> StartupReportAsync(Guid runId,CancellationToken token=default){var q=new GetParameterStartupReportQuery{RunId=runId,Subject=QuerySubject(GetParameterStartupReportQuery.Verb)};return RequestAsync<GetParameterStartupReportQuery,ParameterSignalStartupReport>(q.Subject,q,token).AsTask();}
 public Task<ServiceResult<ParameterSignalMonitoringSnapshot>> SignalMonitoringAsync(Guid runId,CancellationToken token=default){var q=new GetParameterSignalMonitoringQuery{RunId=runId,Subject=QuerySubject(GetParameterSignalMonitoringQuery.Verb)};return RequestAsync<GetParameterSignalMonitoringQuery,ParameterSignalMonitoringSnapshot>(q.Subject,q,token).AsTask();}
 public Task<ServiceResult<ParameterLegacyVersion[]>> LegacyVersionsAsync(int offset=0,CancellationToken token=default){var q=new ListLegacyParameterVersionsQuery{Offset=offset,Subject=QuerySubject(ListLegacyParameterVersionsQuery.Verb)};return RequestAsync<ListLegacyParameterVersionsQuery,ParameterLegacyVersion[]>(q.Subject,q,token).AsTask();}
 public Task<ServiceResult<CreateParameterSetCommand>> PreviewLegacyMigrationAsync(Guid setId,int version,CancellationToken token=default){var q=new PreviewLegacyParameterMigrationQuery{SetId=setId,Version=version,Subject=QuerySubject(PreviewLegacyParameterMigrationQuery.Verb)};return RequestAsync<PreviewLegacyParameterMigrationQuery,CreateParameterSetCommand>(q.Subject,q,token).AsTask();}
 static ActorSubject QuerySubject(string verb)=>new(ActorType.Query,"ParameterSetQuery",verb,ActorEntityId.Default.Format());
 public Task<ServiceResult<ParameterComponentSummary[]>> ComponentsAsync(CancellationToken token=default){var q=new ListParameterComponentsQuery{Subject=QuerySubject(ListParameterComponentsQuery.Verb)};return RequestAsync<ListParameterComponentsQuery,ParameterComponentSummary[]>(q.Subject,q,token).AsTask();}
 public async Task<ServiceResult<ParameterSetVersion[]>> VersionsAsync(Guid? setId=null,CancellationToken token=default,string componentCode=ParameterSchemaRegistry.RegimeComponent)
 {
  var q=new ListParameterVersionsQuery{ComponentCode=componentCode,SetId=setId??Guid.Empty,Subject=QuerySubject(ListParameterVersionsQuery.Verb)};
  var versions=new List<ParameterSetVersion>();
  for(var page=0;page<100;page++)
  {
   var result=await RequestAsync<ListParameterVersionsQuery,ParameterSetVersion[]>(q.Subject,q,token);
   if(!result.Success||result.Value is null)return result;
   versions.AddRange(result.Value);
   if(result.Value.Length<q.Limit)return new ServiceOk<ParameterSetVersion[]>(versions.ToArray());
   var last=result.Value[^1];q=q with {AfterName=last.Name,AfterSetId=last.Reference.SetId,AfterVersion=last.Reference.Version};
  }
  return new ServiceFailed<ParameterSetVersion[]>(33101,"Parameter listing exceeds 10000 versions. Select a specific set.");
 }

 public Task<ServiceResult<ParameterSchemaDefinition>> SchemaAsync(int schemaVersion,CancellationToken token=default){var q=new GetParameterSchemaQuery{SchemaVersion=schemaVersion,Subject=QuerySubject(GetParameterSchemaQuery.Verb)};return RequestAsync<GetParameterSchemaQuery,ParameterSchemaDefinition>(q.Subject,q,token).AsTask();}
 public Task<ServiceResult<string>> PreviewAsync(Guid setId,CancellationToken token=default,int targetHorizon=(int)TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily,string? sourcePayloadJson=null,bool rebuildIntervals=false){var q=new CreateParameterDraftPreviewQuery{SetId=setId,TargetHorizon=targetHorizon,PayloadJson=sourcePayloadJson??"{}",RebuildIntervals=rebuildIntervals,Subject=QuerySubject(CreateParameterDraftPreviewQuery.Verb)};return RequestAsync<CreateParameterDraftPreviewQuery,string>(q.Subject,q,token).AsTask();}
 public Task<ServiceResult<ParameterValidationReport>> ValidateAsync(string json,int schemaVersion,CancellationToken token=default){var q=new ValidateParameterCandidateQuery{PayloadJson=json,SchemaVersion=schemaVersion,Subject=QuerySubject(ValidateParameterCandidateQuery.Verb)};return RequestAsync<ValidateParameterCandidateQuery,ParameterValidationReport>(q.Subject,q,token).AsTask();}
 public Task<ServiceResult<GuidResult>> CreateAsync(CreateParameterSetCommand command,CancellationToken token=default)
 {var c=command with {Subject=new ActorSubject(ActorType.Command,CreateParameterSetCommand.Actor,CreateParameterSetCommand.Verb,command.EntityId.Format())};
 return RequestCommandResultAsync<CreateParameterSetCommand,ParameterSetEntityId,GuidResult>(c,c.EntityId,token).AsTask();}
 public Task<ServiceResult<GuidResult>> SaveAsync(SaveParameterDraftCommand command,CancellationToken token=default)
 {var c=command with {Subject=new ActorSubject(ActorType.Command,SaveParameterDraftCommand.Actor,SaveParameterDraftCommand.Verb,command.EntityId.Format())};
 return RequestCommandResultAsync<SaveParameterDraftCommand,ParameterSetEntityId,GuidResult>(c,c.EntityId,token).AsTask();}
 public Task<ServiceResult<GuidResult>> PublishAsync(PublishParameterVersionCommand command,CancellationToken token=default)
 {var c=command with {Subject=new ActorSubject(ActorType.Command,PublishParameterVersionCommand.Actor,PublishParameterVersionCommand.Verb,command.EntityId.Format())};
 return RequestCommandResultAsync<PublishParameterVersionCommand,ParameterSetEntityId,GuidResult>(c,c.EntityId,token).AsTask();}
 public Task<ServiceResult<ParameterSetSnapshot>> StateAsync(Guid setId,CancellationToken token=default)
 {var q=new GetParameterSetStateQuery{SetId=setId,Subject=QuerySubject(GetParameterSetStateQuery.Verb)};return RequestAsync<GetParameterSetStateQuery,ParameterSetSnapshot>(q.Subject,q,token).AsTask();}
 public Task<ServiceResult<GuidResult>> RenameAsync(RenameParameterSetCommand command,CancellationToken token=default)
 {var c=command with {Subject=new ActorSubject(ActorType.Command,RenameParameterSetCommand.Actor,RenameParameterSetCommand.Verb,command.EntityId.Format())};
 return RequestCommandResultAsync<RenameParameterSetCommand,ParameterSetEntityId,GuidResult>(c,c.EntityId,token).AsTask();}
 public Task<ServiceResult<ParameterAssignmentSnapshot>> AssignmentAsync(string workflowDefinitionId,int targetHorizon,CancellationToken token=default)
 {var q=new GetParameterAssignmentQuery{WorkflowDefinitionId=workflowDefinitionId,TargetHorizon=targetHorizon,Subject=QuerySubject(GetParameterAssignmentQuery.Verb)};return RequestAsync<GetParameterAssignmentQuery,ParameterAssignmentSnapshot>(q.Subject,q,token).AsTask();}
 public Task<ServiceResult<GuidResult>> AssignAsync(AssignParameterVersionCommand command,CancellationToken token=default)
 {var c=command with {Subject=new ActorSubject(ActorType.Command,AssignParameterVersionCommand.Actor,AssignParameterVersionCommand.Verb,command.EntityId.Format())};return RequestCommandResultAsync<AssignParameterVersionCommand,ParameterAssignmentEntityId,GuidResult>(c,c.EntityId,token).AsTask();}
 public Task<ServiceResult<GuidResult>> DisableAssignmentAsync(DisableParameterAssignmentCommand command,CancellationToken token=default)
 {var c=command with {Subject=new ActorSubject(ActorType.Command,DisableParameterAssignmentCommand.Actor,DisableParameterAssignmentCommand.Verb,command.EntityId.Format())};return RequestCommandResultAsync<DisableParameterAssignmentCommand,ParameterAssignmentEntityId,GuidResult>(c,c.EntityId,token).AsTask();}
 public Task<ServiceResult<GuidResult>> RetireAsync(RetireParameterVersionCommand command,CancellationToken token=default)
 {var c=command with {Subject=new ActorSubject(ActorType.Command,RetireParameterVersionCommand.Actor,RetireParameterVersionCommand.Verb,command.EntityId.Format())};return RequestCommandResultAsync<RetireParameterVersionCommand,ParameterSetEntityId,GuidResult>(c,c.EntityId,token).AsTask();}
}
