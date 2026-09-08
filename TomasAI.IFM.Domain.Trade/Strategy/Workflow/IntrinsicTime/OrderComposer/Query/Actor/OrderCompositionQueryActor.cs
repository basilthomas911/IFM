using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query;
using TomasAI.IFM.Framework.Serialization;
using System.Collections.Frozen;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Actor;
public sealed class OrderCompositionQueryActor(IQueryActorContext<OrderCompositionQueryActor> context):BaseQueryActor<OrderCompositionQueryActor>(context,Typed(context).Logger)
{
    public const string ActorName=GetOrderCompositionInvocationQuery.Actor;
    readonly IOrderCompositionQueryContext services=Typed(context);
    static readonly IReadOnlyDictionary<string,Func<IActorMessage,IQuery>> _parseMap=new Dictionary<string,Func<IActorMessage,IQuery>>(StringComparer.Ordinal)
    {
        [GetOrderCompositionInvocationQuery.Verb]=m=>m.AsQuery<GetOrderCompositionInvocationQuery,OrderCompositionProjection>()!,
        [GetOrderCompositionResultQuery.Verb]=m=>m.AsQuery<GetOrderCompositionResultQuery,OrderCompositionResult>()!,
        [GetOrderCompositionHistoryPageQuery.Verb]=m=>m.AsQuery<GetOrderCompositionHistoryPageQuery,OrderCompositionHistoryPage>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type,Func<OrderCompositionQueryActor,IQueryActorContext<OrderCompositionQueryActor>,IQuery,CancellationToken,ValueTask>> _receiveMap=new Dictionary<Type,Func<OrderCompositionQueryActor,IQueryActorContext<OrderCompositionQueryActor>,IQuery,CancellationToken,ValueTask>>
    {
        [typeof(GetOrderCompositionInvocationQuery)]=static(a,c,q,t)=>((GetOrderCompositionInvocationQuery)q).ExecuteAsync(a.services,c,t),
        [typeof(GetOrderCompositionResultQuery)]=static(a,c,q,t)=>((GetOrderCompositionResultQuery)q).ExecuteAsync(a.services,c,t),
        [typeof(GetOrderCompositionHistoryPageQuery)]=static(a,c,q,t)=>((GetOrderCompositionHistoryPageQuery)q).ExecuteAsync(a.services,c,t),
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,QueryExceptionHandler> _exceptionMap=CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<OrderCompositionQueryActor> c,IActorMessage m)=>ParseMappedQuery(c,m,_parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<OrderCompositionQueryActor> c,IQuery q)=>ReceiveAsync(c,q,CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<OrderCompositionQueryActor> c,IQuery q,CancellationToken t)=>ResolveMappedQueryHandler(q,_receiveMap)(this,c,q,t);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<OrderCompositionQueryActor> c,ActorThreadId id,IQuery q,string verb,Exception ex)=>ExceptionMappedQueryAsync(c,id,q,verb,ex,_exceptionMap);
    static IOrderCompositionQueryContext Typed(IQueryActorContext<OrderCompositionQueryActor> c)=>c as IOrderCompositionQueryContext??throw new ArgumentException("Typed composition query context required.");
}
