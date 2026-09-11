using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
public interface IParameterSetsApi
{
 Task<ServiceResult<ParameterLegacyVersion[]>> LegacyVersionsAsync(int offset=0,CancellationToken token=default);
 Task<ServiceResult<CreateParameterSetCommand>> PreviewLegacyMigrationAsync(Guid setId,int version,CancellationToken token=default);
 Task<ServiceResult<ParameterSignalMonitoringSnapshot>> SignalMonitoringAsync(Guid runId,CancellationToken token=default);
 Task<ServiceResult<ParameterSignalStartupReport>> StartupReportAsync(Guid runId,CancellationToken token=default);
 Task<ServiceResult<GuidResult>> RecordStartupReportAsync(RecordSignalStartupReportCommand command,CancellationToken token=default);
 Task<ServiceResult<ParameterStartupRun[]>> StartupRunsAsync(CancellationToken token=default);
 Task<ServiceResult<GuidResult>> ApplyStartupAsync(ApplySignalStartupPlanCommand command,CancellationToken token=default);
 Task<ServiceResult<GuidResult>> ReleaseStartupAsync(ReleaseSignalStartupPlanCommand command,CancellationToken token=default);
 Task<ServiceResult<ParameterSignalStartupPlan>> PreviewStartupAsync(Guid runId,CancellationToken token=default);
 Task<ServiceResult<ParameterSchemaDefinition>> SchemaAsync(int schemaVersion,CancellationToken token=default);
 Task<ServiceResult<ParameterComponentSummary[]>> ComponentsAsync(CancellationToken token=default);
 Task<ServiceResult<ParameterSetVersion[]>> VersionsAsync(Guid? setId=null,CancellationToken token=default,string componentCode=ParameterSchemaRegistry.RegimeComponent);
 Task<ServiceResult<string>> PreviewAsync(Guid setId,CancellationToken token=default,int targetHorizon=(int)TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily,string? sourcePayloadJson=null,bool rebuildIntervals=false);
 Task<ServiceResult<ParameterValidationReport>> ValidateAsync(string json,int schemaVersion,CancellationToken token=default);
 Task<ServiceResult<GuidResult>> CreateAsync(CreateParameterSetCommand command,CancellationToken token=default);
 Task<ServiceResult<GuidResult>> SaveAsync(SaveParameterDraftCommand command,CancellationToken token=default);
 Task<ServiceResult<ParameterSetSnapshot>> StateAsync(Guid setId,CancellationToken token=default);
 Task<ServiceResult<GuidResult>> RetireAsync(RetireParameterVersionCommand command,CancellationToken token=default);
 Task<ServiceResult<GuidResult>> RenameAsync(RenameParameterSetCommand command,CancellationToken token=default);
 Task<ServiceResult<GuidResult>> PublishAsync(PublishParameterVersionCommand command,CancellationToken token=default);
 Task<ServiceResult<ParameterAssignmentSnapshot>> AssignmentAsync(string workflowDefinitionId,int targetHorizon,CancellationToken token=default);
 Task<ServiceResult<GuidResult>> AssignAsync(AssignParameterVersionCommand command,CancellationToken token=default);
 Task<ServiceResult<GuidResult>> DisableAssignmentAsync(DisableParameterAssignmentCommand command,CancellationToken token=default);
}
