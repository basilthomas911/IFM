using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
public sealed class FuturesOptionPositionQueryActor(IQueryActorContext<FuturesOptionPositionQueryActor> context) : BaseQueryActor<FuturesOptionPositionQueryActor>(context, T(context).Logger)
{
    public const string ActorName = PositionActorNames.Query;
    readonly IFuturesOptionPositionQueryContext s = T(context);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>
(StringComparer.Ordinal)    {
        {
            GetIronCondorOptionTradePositionQuery.Verb,
            m => m.AsQuery<GetIronCondorOptionTradePositionQuery, StrategyPositionSnapshot>()!
        },
        {
            GetVerticalSpreadOptionTradePositionQuery.Verb,
            m => m.AsQuery<GetVerticalSpreadOptionTradePositionQuery, StrategyPositionSnapshot>()!
        },
        {
            GetIronCondorOptionTradePositionHistoryQuery.Verb,
            m => m.AsQuery<GetIronCondorOptionTradePositionHistoryQuery, StrategyPositionSnapshot[]>()!
        },
        {
            GetVerticalSpreadOptionTradePositionHistoryQuery.Verb,
            m => m.AsQuery<GetVerticalSpreadOptionTradePositionHistoryQuery, StrategyPositionSnapshot[]>()!
        }
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<IFuturesOptionPositionQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IFuturesOptionPositionQueryContext, IQuery, CancellationToken, ValueTask>>
    {
        {
            typeof(GetIronCondorOptionTradePositionQuery),
            static (c, q, t) => ((GetIronCondorOptionTradePositionQuery)q).ExecuteAsync(c, t)
        },
        {
            typeof(GetVerticalSpreadOptionTradePositionQuery),
            static (c, q, t) => ((GetVerticalSpreadOptionTradePositionQuery)q).ExecuteAsync(c, t)
        },
        {
            typeof(GetIronCondorOptionTradePositionHistoryQuery),
            static (c, q, t) => ((GetIronCondorOptionTradePositionHistoryQuery)q).ExecuteAsync(c, t)
        },
        {
            typeof(GetVerticalSpreadOptionTradePositionHistoryQuery),
            static (c, q, t) => ((GetVerticalSpreadOptionTradePositionHistoryQuery)q).ExecuteAsync(c, t)
        }
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<FuturesOptionPositionQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesOptionPositionQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesOptionPositionQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(s, query, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<FuturesOptionPositionQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static IFuturesOptionPositionQueryContext T(IQueryActorContext<FuturesOptionPositionQueryActor> c) => c as IFuturesOptionPositionQueryContext ?? throw new ArgumentException("Typed Futures Option Position query context required.", nameof(c));
}
