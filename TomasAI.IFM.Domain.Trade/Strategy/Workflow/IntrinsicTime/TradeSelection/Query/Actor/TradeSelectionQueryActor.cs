using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query;
using TomasAI.IFM.Framework.Serialization;
using System.Collections.Frozen;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Actor;
public sealed class TradeSelectionQueryActor(IQueryActorContext<TradeSelectionQueryActor> context):BaseQueryActor<TradeSelectionQueryActor>(context,Typed(context).Logger)
{
    public const string ActorName=GetTradeSelectionInvocationQuery.Actor;
    readonly ITradeSelectionQueryContext services=Typed(context);
    static readonly IReadOnlyDictionary<string,Func<IActorMessage,IQuery>> _parseMap=new Dictionary<string,Func<IActorMessage,IQuery>>(StringComparer.Ordinal)
    {
        [GetTradeSelectionInvocationQuery.Verb]=m=>m.AsQuery<GetTradeSelectionInvocationQuery,TradeSelectionProjection>()!,
        [GetTradeSelectionResultQuery.Verb]=m=>m.AsQuery<GetTradeSelectionResultQuery,TradeSelectionResult>()!,
        [GetTradeSelectionHistoryPageQuery.Verb]=m=>m.AsQuery<GetTradeSelectionHistoryPageQuery,TradeSelectionHistoryPage>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ITradeSelectionQueryContext, IQueryActorContext<TradeSelectionQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<ITradeSelectionQueryContext, IQueryActorContext<TradeSelectionQueryActor>, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetTradeSelectionInvocationQuery)] = static (services, context, query, cancellationToken) => ((GetTradeSelectionInvocationQuery)query).ExecuteAsync(services, context, cancellationToken),
        [typeof(GetTradeSelectionResultQuery)] = static (services, context, query, cancellationToken) => ((GetTradeSelectionResultQuery)query).ExecuteAsync(services, context, cancellationToken),
        [typeof(GetTradeSelectionHistoryPageQuery)] = static (services, context, query, cancellationToken) => ((GetTradeSelectionHistoryPageQuery)query).ExecuteAsync(services, context, cancellationToken)
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<TradeSelectionQueryActor> c,IActorMessage m)=>ParseMappedQuery(c,m,_parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<TradeSelectionQueryActor> c,IQuery q)=>ReceiveAsync(c,q,CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<TradeSelectionQueryActor> c,IQuery q,CancellationToken t)=>ResolveMappedQueryHandler(q,_receiveMap)(services,c,q,t);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<TradeSelectionQueryActor> c,ActorThreadId id,IQuery q,string verb,Exception ex)=>ExceptionMappedQueryAsync(c,id,q,verb,ex,_exceptionMap);
    static ITradeSelectionQueryContext Typed(IQueryActorContext<TradeSelectionQueryActor> c)=>c as ITradeSelectionQueryContext??throw new ArgumentException("Typed selector query context required.");
}
