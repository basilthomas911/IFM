using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Query.Actor;
public sealed class FuturesPositionQueryActor(IQueryActorContext<FuturesPositionQueryActor> context) : BaseQueryActor<FuturesPositionQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = FuturesPositionActorNames.Query;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetFuturesTradePositionQuery.Verb] = message => message.AsQuery<GetFuturesTradePositionQuery, StrategyPositionSnapshot>()!,
        [GetFuturesTradePositionHistoryQuery.Verb] = message => message.AsQuery<GetFuturesTradePositionHistoryQuery, StrategyPositionSnapshot[]>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<IFuturesPositionQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IFuturesPositionQueryContext, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetFuturesTradePositionQuery)] = static (context, query, token) => ((GetFuturesTradePositionQuery)query).ExecuteAsync(context, token),
        [typeof(GetFuturesTradePositionHistoryQuery)] = static (context, query, token) => ((GetFuturesTradePositionHistoryQuery)query).ExecuteAsync(context, token)
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<FuturesPositionQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesPositionQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesPositionQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(Typed(context), query, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<FuturesPositionQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static IFuturesPositionQueryContext Typed(IQueryActorContext<FuturesPositionQueryActor> context) => context as IFuturesPositionQueryContext ?? throw new ArgumentException("Typed Futures position query context required.", nameof(context));
}
