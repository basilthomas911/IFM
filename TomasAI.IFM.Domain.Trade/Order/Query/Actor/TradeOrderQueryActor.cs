using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Order.Query.Actor;
public sealed class TradeOrderQueryActor(IQueryActorContext<TradeOrderQueryActor> context) : BaseQueryActor<TradeOrderQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = TradeOrderActorNames.Query;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetTradeEndOfDayDisplayQuery.Verb] = m => m.AsQuery<GetTradeEndOfDayDisplayQuery, TradeEndOfDayDisplay>()!,
        [PrepareTradeOrderQuery.Verb] = m => m.AsQuery<PrepareTradeOrderQuery, TradeOrderPreparation>()!,
        [GetTradeOrderPreviewQuery.Verb] = m => m.AsQuery<GetTradeOrderPreviewQuery, TradeOrderPreview>()!,
        [GetTradeOrderQuery.Verb] = m => m.AsQuery<GetTradeOrderQuery, TradeOrderDefinition>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ITradeOrderQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<ITradeOrderQueryContext, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetTradeEndOfDayDisplayQuery)] = static (c,q,t) => ((GetTradeEndOfDayDisplayQuery)q).ExecuteAsync(c,t),
        [typeof(PrepareTradeOrderQuery)] = static (c, q, t) => ((PrepareTradeOrderQuery)q).ExecuteAsync(c, t),
        [typeof(GetTradeOrderPreviewQuery)] = static (c, q, t) => ((GetTradeOrderPreviewQuery)q).ExecuteAsync(c, t),
        [typeof(GetTradeOrderQuery)] = static (c, q, t) => ((GetTradeOrderQuery)q).ExecuteAsync(c, t)
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<TradeOrderQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<TradeOrderQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<TradeOrderQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(Typed(context), query, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<TradeOrderQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static ITradeOrderQueryContext Typed(IQueryActorContext<TradeOrderQueryActor> c) => c as ITradeOrderQueryContext ?? throw new ArgumentException("Typed Trade Order query context required.", nameof(c));
}
