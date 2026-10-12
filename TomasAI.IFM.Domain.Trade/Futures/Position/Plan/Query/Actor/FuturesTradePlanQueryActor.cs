using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Query.Actor;
/// <summary>Serves current and historical outright Futures Trade Plans.</summary>
public sealed class FuturesTradePlanQueryActor(IQueryActorContext<FuturesTradePlanQueryActor> context) : BaseQueryActor<FuturesTradePlanQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetCurrentFuturesTradePlanQuery.Actor;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetCurrentFuturesTradePlanQuery.Verb] = message => message.AsQuery<GetCurrentFuturesTradePlanQuery, StrategyTradePlanSnapshot?>()!,
        [GetFuturesTradePlanHistoryQuery.Verb] = message => message.AsQuery<GetFuturesTradePlanHistoryQuery, StrategyTradePlanHistoryPage>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<IFuturesTradePlanQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IFuturesTradePlanQueryContext, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetCurrentFuturesTradePlanQuery)] = static (c, q, t) => ((GetCurrentFuturesTradePlanQuery)q).ExecuteAsync(c, t),
        [typeof(GetFuturesTradePlanHistoryQuery)] = static (c, q, t) => ((GetFuturesTradePlanHistoryQuery)q).ExecuteAsync(c, t)
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<FuturesTradePlanQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesTradePlanQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesTradePlanQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(Typed(context), query, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<FuturesTradePlanQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static IFuturesTradePlanQueryContext Typed(IQueryActorContext<FuturesTradePlanQueryActor> context) => context as IFuturesTradePlanQueryContext ?? throw new ArgumentException("Typed Futures Trade Plan query context required.", nameof(context));
}
