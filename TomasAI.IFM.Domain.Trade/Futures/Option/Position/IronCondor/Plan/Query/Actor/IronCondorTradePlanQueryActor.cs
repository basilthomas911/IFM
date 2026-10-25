using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.Actor;
/// <summary>Serves current and historical Iron Condor Trade Plans.</summary>
public sealed class IronCondorTradePlanQueryActor(IQueryActorContext<IronCondorTradePlanQueryActor> context) : BaseQueryActor<IronCondorTradePlanQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetCurrentIronCondorTradePlanQuery.Actor;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetCurrentIronCondorTradePlanQuery.Verb] = message => message.AsQuery<GetCurrentIronCondorTradePlanQuery, StrategyTradePlanSnapshot?>()!,
        [GetIronCondorTradePlanChartQuery.Verb] = message => message.AsQuery<GetIronCondorTradePlanChartQuery, StrategyTradePlanChartSeries>()!,
        [GetIronCondorTradePlanHistoryQuery.Verb] = message => message.AsQuery<GetIronCondorTradePlanHistoryQuery, StrategyTradePlanHistoryPage>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<IIronCondorTradePlanQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IIronCondorTradePlanQueryContext, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetCurrentIronCondorTradePlanQuery)] = static (c, q, t) => ((GetCurrentIronCondorTradePlanQuery)q).ExecuteAsync(c, t),
        [typeof(GetIronCondorTradePlanChartQuery)] = static (c, q, t) => ((GetIronCondorTradePlanChartQuery)q).ExecuteAsync(c, t),
        [typeof(GetIronCondorTradePlanHistoryQuery)] = static (c, q, t) => ((GetIronCondorTradePlanHistoryQuery)q).ExecuteAsync(c, t)
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<IronCondorTradePlanQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<IronCondorTradePlanQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<IronCondorTradePlanQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(Typed(context), query, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<IronCondorTradePlanQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static IIronCondorTradePlanQueryContext Typed(IQueryActorContext<IronCondorTradePlanQueryActor> context) => context as IIronCondorTradePlanQueryContext ?? throw new ArgumentException("Typed Iron Condor Trade Plan query context required.", nameof(context));
}
