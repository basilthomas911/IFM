using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Query;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query;
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
        [typeof(PreviewSignalStartupPlanQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((PreviewSignalStartupPlanQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, PreviewSignalStartupPlanQuery.Verb,
                new ServiceOk<ParameterSignalStartupPlan>(result)).ConfigureAwait(false);
        },
        [typeof(GetParameterStartupRunQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((GetParameterStartupRunQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetParameterStartupRunQuery.Verb,
                new ServiceOk<ParameterStartupRun>(result)).ConfigureAwait(false);
        },
        [typeof(GetParameterStartupRunsQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((GetParameterStartupRunsQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetParameterStartupRunsQuery.Verb,
                new ServiceOk<ParameterStartupRun[]>(result)).ConfigureAwait(false);
        },
        [typeof(GetParameterStartupReportQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((GetParameterStartupReportQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetParameterStartupReportQuery.Verb,
                new ServiceOk<ParameterSignalStartupReport>(result)).ConfigureAwait(false);
        },
        [typeof(ListLegacyParameterVersionsQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((ListLegacyParameterVersionsQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, ListLegacyParameterVersionsQuery.Verb,
                new ServiceOk<ParameterLegacyVersion[]>(result)).ConfigureAwait(false);
        },
        [typeof(PreviewLegacyParameterMigrationQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((PreviewLegacyParameterMigrationQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, PreviewLegacyParameterMigrationQuery.Verb,
                new ServiceOk<CreateParameterSetCommand>(result)).ConfigureAwait(false);
        },
        [typeof(GetParameterSignalMonitoringQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((GetParameterSignalMonitoringQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetParameterSignalMonitoringQuery.Verb,
                new ServiceOk<ParameterSignalMonitoringSnapshot>(result)).ConfigureAwait(false);
        },
        [typeof(GetParameterSchemaQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((GetParameterSchemaQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetParameterSchemaQuery.Verb,
                new ServiceOk<ParameterSchemaDefinition>(result)).ConfigureAwait(false);
        },
        [typeof(GetParameterAssignmentQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((GetParameterAssignmentQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetParameterAssignmentQuery.Verb,
                new ServiceOk<ParameterAssignmentSnapshot>(result)).ConfigureAwait(false);
        },
        [typeof(GetParameterSetStateQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((GetParameterSetStateQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetParameterSetStateQuery.Verb,
                new ServiceOk<ParameterSetSnapshot>(result)).ConfigureAwait(false);
        },
        [typeof(ListParameterComponentsQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((ListParameterComponentsQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, ListParameterComponentsQuery.Verb,
                new ServiceOk<ParameterComponentSummary[]>(result)).ConfigureAwait(false);
        },
        [typeof(ListParameterVersionsQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((ListParameterVersionsQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, ListParameterVersionsQuery.Verb,
                new ServiceOk<ParameterSetVersion[]>(result)).ConfigureAwait(false);
        },
        [typeof(PreviewTypedParameterDraftQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((PreviewTypedParameterDraftQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, PreviewTypedParameterDraftQuery.Verb,
                new ServiceOk<ParameterSetValue>(result)).ConfigureAwait(false);
        },
        [typeof(ValidateTypedParameterCandidateQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((ValidateTypedParameterCandidateQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, ValidateTypedParameterCandidateQuery.Verb,
                new ServiceOk<ParameterValidationReport>(result)).ConfigureAwait(false);
        },
    };
    protected override IQuery ParseMessage(IQueryActorContext<ParameterSetQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<ParameterSetQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override async ValueTask ReceiveAsync(IQueryActorContext<ParameterSetQueryActor> context, IQuery query, CancellationToken cancellationToken) => await (ResolveMappedQueryHandler(query, _receiveMap)(query, (IParameterSetQueryContext)context, cancellationToken)).ConfigureAwait(false);
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<ParameterSetQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
}
