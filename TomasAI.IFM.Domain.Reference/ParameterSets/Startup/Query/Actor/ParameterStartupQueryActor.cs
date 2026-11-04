using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;

public sealed class ParameterStartupQueryActor(IQueryActorContext<ParameterStartupQueryActor> context) : BaseQueryActor<ParameterStartupQueryActor>(context, ((IParameterStartupQueryContext)context).Logger)
{
    public const string ActorName = "ParameterStartupQuery";
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [PreviewSignalStartupPlanQuery.Verb] = m => m.AsQuery<PreviewSignalStartupPlanQuery, ParameterSignalStartupPlan>()!,
        [GetParameterStartupRunQuery.Verb] = m => m.AsQuery<GetParameterStartupRunQuery, ParameterStartupRun>()!,
        [GetParameterStartupRunsQuery.Verb] = m => m.AsQuery<GetParameterStartupRunsQuery, ParameterStartupRun[]>()!,
        [GetParameterStartupReportQuery.Verb] = m => m.AsQuery<GetParameterStartupReportQuery, ParameterSignalStartupReport>()!,
        [GetParameterSignalMonitoringQuery.Verb] = m => m.AsQuery<GetParameterSignalMonitoringQuery, ParameterSignalMonitoringSnapshot>()!,
    };
    static readonly IReadOnlyDictionary<Type, Func<IQuery, IParameterStartupQueryContext, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IQuery, IParameterStartupQueryContext, CancellationToken, ValueTask>>
    {
        [typeof(PreviewSignalStartupPlanQuery)] = (q, ctx, ct) => ((PreviewSignalStartupPlanQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(GetParameterStartupRunQuery)] = (q, ctx, ct) => ((GetParameterStartupRunQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(GetParameterStartupRunsQuery)] = (q, ctx, ct) => ((GetParameterStartupRunsQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(GetParameterStartupReportQuery)] = (q, ctx, ct) => ((GetParameterStartupReportQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
        [typeof(GetParameterSignalMonitoringQuery)] = (q, ctx, ct) => ((GetParameterSignalMonitoringQuery)q).ExecuteAsync(ctx, ctx.Logger, ct),
    };
    protected override IQuery ParseMessage(IQueryActorContext<ParameterStartupQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<ParameterStartupQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override async ValueTask ReceiveAsync(IQueryActorContext<ParameterStartupQueryActor> context, IQuery query, CancellationToken cancellationToken) => await (ResolveMappedQueryHandler(query, _receiveMap)(query, (IParameterStartupQueryContext)context, cancellationToken)).ConfigureAwait(false);
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<ParameterStartupQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
}
