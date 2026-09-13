using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Order.Query.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Order.Query.Actor;

public sealed class TradeOrderQueryActor(IQueryActorContext<TradeOrderQueryActor> context)
    : BaseQueryActor<TradeOrderQueryActor>(context,Typed(context).Logger)
{
    public const string ActorName = TradeOrderActorNames.Query;
    static readonly IReadOnlyDictionary<string,Func<IActorMessage,IQuery>> _parseMap = new Dictionary<string,Func<IActorMessage,IQuery>>(StringComparer.Ordinal)
    { [GetTradeOrderQuery.Verb] = m => m.AsQuery<GetTradeOrderQuery,TradeOrderDefinition>()! }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type,Func<ITradeOrderQueryContext,IQuery,CancellationToken,ValueTask>> _receiveMap =
        new Dictionary<Type,Func<ITradeOrderQueryContext,IQuery,CancellationToken,ValueTask>>
        { [typeof(GetTradeOrderQuery)] = static (c,q,t) => ((GetTradeOrderQuery)q).ExecuteAsync(c,t) }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,QueryExceptionHandler> _exceptionMap=CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<TradeOrderQueryActor> c,IActorMessage m)=>ParseMappedQuery(c,m,_parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<TradeOrderQueryActor> c,IQuery q)=>ReceiveAsync(c,q,CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<TradeOrderQueryActor> c,IQuery q,CancellationToken t)=>ResolveMappedQueryHandler(q,_receiveMap)(Typed(c),q,t);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<TradeOrderQueryActor> c,ActorThreadId id,IQuery q,string verb,Exception ex)=>ExceptionMappedQueryAsync(c,id,q,verb,ex,_exceptionMap);
    static ITradeOrderQueryContext Typed(IQueryActorContext<TradeOrderQueryActor> c)=>c as ITradeOrderQueryContext??throw new ArgumentException("Typed Trade Order query context required.");
}

public interface ITradeOrderQueryContext : IQueryActorContext<TradeOrderQueryActor> { IDbContextFactory DbFactory { get; } ILogger<TradeOrderQueryActor> Logger { get; } }
public sealed class TradeOrderQueryContext(IActorSupervisor supervisor,IDbContextFactory dbFactory,ILogger<TradeOrderQueryActor> logger)
    : QueryActorContext(supervisor,new ActorMailboxId(ActorType.Query,TradeOrderQueryActor.ActorName)),IQueryActorContext<TradeOrderQueryActor>,ITradeOrderQueryContext
{ public IDbContextFactory DbFactory { get; }=dbFactory; public ILogger<TradeOrderQueryActor> Logger { get; }=logger; }
