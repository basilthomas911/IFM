using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
public sealed class ParameterSetQueryActor(IQueryActorContext<ParameterSetQueryActor> context):BaseQueryActor<ParameterSetQueryActor>(context,((IParameterSetQueryContext)context).Logger)
{
 public const string ActorName="ParameterSetQuery";
 static readonly IReadOnlyDictionary<string,Func<IActorMessage,IQuery>> _parseMap=new Dictionary<string,Func<IActorMessage,IQuery>>(StringComparer.Ordinal)
 {
 [PreviewSignalStartupPlanQuery.Verb]=m=>m.AsQuery<PreviewSignalStartupPlanQuery,ParameterSignalStartupPlan>()!,
 [GetParameterStartupRunsQuery.Verb]=m=>m.AsQuery<GetParameterStartupRunsQuery,ParameterStartupRun[]>()!,
 [GetParameterStartupReportQuery.Verb]=m=>m.AsQuery<GetParameterStartupReportQuery,ParameterSignalStartupReport>()!,
 [ListLegacyParameterVersionsQuery.Verb]=m=>m.AsQuery<ListLegacyParameterVersionsQuery,ParameterLegacyVersion[]>()!,
 [PreviewLegacyParameterMigrationQuery.Verb]=m=>m.AsQuery<PreviewLegacyParameterMigrationQuery,CreateParameterSetCommand>()!,
 [GetParameterSignalMonitoringQuery.Verb]=m=>m.AsQuery<GetParameterSignalMonitoringQuery,ParameterSignalMonitoringSnapshot>()!,
 [GetParameterSchemaQuery.Verb]=m=>m.AsQuery<GetParameterSchemaQuery,ParameterSchemaDefinition>()!,
 [GetParameterAssignmentQuery.Verb]=m=>m.AsQuery<GetParameterAssignmentQuery,ParameterAssignmentSnapshot>()!,
 [GetParameterSetStateQuery.Verb]=m=>m.AsQuery<GetParameterSetStateQuery,ParameterSetSnapshot>()!,
 [ListParameterComponentsQuery.Verb]=m=>m.AsQuery<ListParameterComponentsQuery,ParameterComponentSummary[]>()!,
 [ListParameterVersionsQuery.Verb]=m=>m.AsQuery<ListParameterVersionsQuery,ParameterSetVersion[]>()!,
 [ValidateParameterCandidateQuery.Verb]=m=>m.AsQuery<ValidateParameterCandidateQuery,ParameterValidationReport>()!,
 [CreateParameterDraftPreviewQuery.Verb]=m=>m.AsQuery<CreateParameterDraftPreviewQuery,string>()!,
 };
 static readonly IReadOnlyDictionary<Type,Func<IQuery,IParameterSetQueryContext,CancellationToken,ValueTask>> _receiveMap=new Dictionary<Type,Func<IQuery,IParameterSetQueryContext,CancellationToken,ValueTask>>
 {
 [typeof(PreviewSignalStartupPlanQuery)]=(q,ctx,ct)=>((PreviewSignalStartupPlanQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 [typeof(GetParameterStartupRunsQuery)]=(q,ctx,ct)=>((GetParameterStartupRunsQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 [typeof(GetParameterStartupReportQuery)]=(q,ctx,ct)=>((GetParameterStartupReportQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 [typeof(ListLegacyParameterVersionsQuery)]=(q,ctx,ct)=>((ListLegacyParameterVersionsQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 [typeof(PreviewLegacyParameterMigrationQuery)]=(q,ctx,ct)=>((PreviewLegacyParameterMigrationQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 [typeof(GetParameterSignalMonitoringQuery)]=(q,ctx,ct)=>((GetParameterSignalMonitoringQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 [typeof(GetParameterSchemaQuery)]=(q,ctx,ct)=>((GetParameterSchemaQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 [typeof(GetParameterAssignmentQuery)]=(q,ctx,ct)=>((GetParameterAssignmentQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 [typeof(GetParameterSetStateQuery)]=(q,ctx,ct)=>((GetParameterSetStateQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 [typeof(ListParameterComponentsQuery)]=(q,ctx,ct)=>((ListParameterComponentsQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 [typeof(ListParameterVersionsQuery)]=(q,ctx,ct)=>((ListParameterVersionsQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 [typeof(ValidateParameterCandidateQuery)]=(q,ctx,ct)=>((ValidateParameterCandidateQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 [typeof(CreateParameterDraftPreviewQuery)]=(q,ctx,ct)=>((CreateParameterDraftPreviewQuery)q).ExecuteAsync(ctx,ctx.Logger,ct),
 };
 protected override IQuery ParseMessage(IQueryActorContext<ParameterSetQueryActor> ctx,IActorMessage msg)=>ParseMappedQuery(ctx,msg,_parseMap);
 protected override ValueTask ReceiveAsync(IQueryActorContext<ParameterSetQueryActor> ctx,IQuery q)=>ReceiveAsync(ctx,q,CancellationToken.None);
 protected override async ValueTask ReceiveAsync(IQueryActorContext<ParameterSetQueryActor> ctx,IQuery q,CancellationToken token)=>await ResolveMappedQueryHandler(q,_receiveMap)(q,(IParameterSetQueryContext)ctx,token);
 static readonly IReadOnlyDictionary<Type,QueryExceptionHandler> _exceptionMap=CreateQueryExceptionMap(_receiveMap.Keys);
 protected override ValueTask OnExceptionAsync(IQueryActorContext<ParameterSetQueryActor> ctx,ActorThreadId id,IQuery q,string verb,Exception error)=>ExceptionMappedQueryAsync(ctx,id,q,verb,error,_exceptionMap);
}
