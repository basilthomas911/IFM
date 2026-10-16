using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;

public sealed class ParameterSetQueryActor(IQueryActorContext<ParameterSetQueryActor> context) : BaseQueryActor<ParameterSetQueryActor>(context, ((IParameterSetQueryContext)context).Logger)
{
    public const string ActorName = "ParameterSetQuery";
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [PreviewSignalStartupPlanQuery.Verb] = m => m.AsQuery<PreviewSignalStartupPlanQuery, ParameterSignalStartupPlan>()!,
        [GetParameterStartupRunQuery.Verb] = m => m.AsQuery<GetParameterStartupRunQuery, ParameterStartupRun>()!,
        [GetParameterStartupRunsQuery.Verb] = m => m.AsQuery<GetParameterStartupRunsQuery, ParameterStartupRun[]>()!,
        [GetParameterStartupReportQuery.Verb] = m => m.AsQuery<GetParameterStartupReportQuery, ParameterSignalStartupReport>()!,
        [ListLegacyParameterVersionsQuery.Verb] = m => m.AsQuery<ListLegacyParameterVersionsQuery, ParameterLegacyVersion[]>()!,
        [PreviewLegacyParameterMigrationQuery.Verb] = m => m.AsQuery<PreviewLegacyParameterMigrationQuery, CreateParameterSetCommand>()!,
        [GetParameterSignalMonitoringQuery.Verb] = m => m.AsQuery<GetParameterSignalMonitoringQuery, ParameterSignalMonitoringSnapshot>()!,
        [GetParameterSchemaQuery.Verb] = m => m.AsQuery<GetParameterSchemaQuery, ParameterSchemaDefinition>()!,
        [GetParameterAssignmentQuery.Verb] = m => m.AsQuery<GetParameterAssignmentQuery, ParameterAssignmentSnapshot>()!,
        [GetParameterSetStateQuery.Verb] = m => m.AsQuery<GetParameterSetStateQuery, ParameterSetSnapshot>()!,
        [ListParameterComponentsQuery.Verb] = m => m.AsQuery<ListParameterComponentsQuery, ParameterComponentSummary[]>()!,
        [ListParameterVersionsQuery.Verb] = m => m.AsQuery<ListParameterVersionsQuery, ParameterSetVersion[]>()!,
        [PreviewTypedParameterDraftQuery.Verb] = m => m.AsQuery<PreviewTypedParameterDraftQuery, ParameterSetValue>()!,
        [ValidateTypedParameterCandidateQuery.Verb] = m => m.AsQuery<ValidateTypedParameterCandidateQuery, ParameterValidationReport>()!,
    };
    static readonly IReadOnlyDictionary<Type, Func<IQuery, IParameterSetQueryContext, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IQuery, IParameterSetQueryContext, CancellationToken, ValueTask>>
    {
        [typeof(PreviewSignalStartupPlanQuery)] = (q, ctx, ct) => ((PreviewSignalStartupPlanQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(GetParameterStartupRunQuery)] = (q, ctx, ct) => ((GetParameterStartupRunQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(GetParameterStartupRunsQuery)] = (q, ctx, ct) => ((GetParameterStartupRunsQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(GetParameterStartupReportQuery)] = (q, ctx, ct) => ((GetParameterStartupReportQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(ListLegacyParameterVersionsQuery)] = (q, ctx, ct) => ((ListLegacyParameterVersionsQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(PreviewLegacyParameterMigrationQuery)] = (q, ctx, ct) => ((PreviewLegacyParameterMigrationQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(GetParameterSignalMonitoringQuery)] = (q, ctx, ct) => ((GetParameterSignalMonitoringQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(GetParameterSchemaQuery)] = (q, ctx, ct) => ((GetParameterSchemaQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(GetParameterAssignmentQuery)] = (q, ctx, ct) => ((GetParameterAssignmentQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(GetParameterSetStateQuery)] = (q, ctx, ct) => ((GetParameterSetStateQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(ListParameterComponentsQuery)] = (q, ctx, ct) => ((ListParameterComponentsQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(ListParameterVersionsQuery)] = (q, ctx, ct) => ((ListParameterVersionsQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(PreviewTypedParameterDraftQuery)] = (q, ctx, ct) => ((PreviewTypedParameterDraftQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(ValidateTypedParameterCandidateQuery)] = (q, ctx, ct) => ((ValidateTypedParameterCandidateQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
    };
    protected override IQuery ParseMessage(IQueryActorContext<ParameterSetQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<ParameterSetQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override async ValueTask ReceiveAsync(IQueryActorContext<ParameterSetQueryActor> context, IQuery query, CancellationToken cancellationToken) => await (ResolveMappedQueryHandler(query, _receiveMap)(query, (IParameterSetQueryContext)context, cancellationToken)).ConfigureAwait(false);
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<ParameterSetQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
}
