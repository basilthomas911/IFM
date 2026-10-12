using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Query.Actor;
/// <summary>Serves current and historical Vertical Spread Trade Plans.</summary>
public sealed class VerticalSpreadTradePlanQueryActor(IQueryActorContext<VerticalSpreadTradePlanQueryActor> context) : BaseQueryActor<VerticalSpreadTradePlanQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetCurrentVerticalSpreadTradePlanQuery.Actor;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetCurrentVerticalSpreadTradePlanQuery.Verb] = message => message.AsQuery<GetCurrentVerticalSpreadTradePlanQuery, StrategyTradePlanSnapshot?>()!,
        [GetVerticalSpreadTradePlanHistoryQuery.Verb] = message => message.AsQuery<GetVerticalSpreadTradePlanHistoryQuery, StrategyTradePlanHistoryPage>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<IVerticalSpreadTradePlanQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IVerticalSpreadTradePlanQueryContext, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetCurrentVerticalSpreadTradePlanQuery)] = static (c, q, t) => ((GetCurrentVerticalSpreadTradePlanQuery)q).ExecuteAsync(c, t),
        [typeof(GetVerticalSpreadTradePlanHistoryQuery)] = static (c, q, t) => ((GetVerticalSpreadTradePlanHistoryQuery)q).ExecuteAsync(c, t)
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<VerticalSpreadTradePlanQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<VerticalSpreadTradePlanQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<VerticalSpreadTradePlanQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(Typed(context), query, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<VerticalSpreadTradePlanQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static IVerticalSpreadTradePlanQueryContext Typed(IQueryActorContext<VerticalSpreadTradePlanQueryActor> context) => context as IVerticalSpreadTradePlanQueryContext ?? throw new ArgumentException("Typed Vertical Spread Trade Plan query context required.", nameof(context));
}
