using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query;
using TomasAI.IFM.Framework.Serialization;
using System.Collections.Frozen;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Actor;
public sealed class RiskQueryActor(IQueryActorContext<RiskQueryActor> context):BaseQueryActor<RiskQueryActor>(context,Typed(context).Logger)
{
    public const string ActorName=GetRiskInvocationQuery.Actor;
    readonly IRiskQueryContext services=Typed(context);
    static readonly IReadOnlyDictionary<string,Func<IActorMessage,IQuery>> _parseMap=new Dictionary<string,Func<IActorMessage,IQuery>>(StringComparer.Ordinal)
    {
        [GetRiskInvocationQuery.Verb]=m=>m.AsQuery<GetRiskInvocationQuery,RiskObservation>()!,
        [GetRiskResultQuery.Verb]=m=>m.AsQuery<GetRiskResultQuery,RiskAssessmentResult>()!,
        [GetRiskHistoryPageQuery.Verb]=m=>m.AsQuery<GetRiskHistoryPageQuery,RiskHistoryPage>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type,Func<RiskQueryActor,IQueryActorContext<RiskQueryActor>,IQuery,CancellationToken,ValueTask>> _receiveMap=new Dictionary<Type,Func<RiskQueryActor,IQueryActorContext<RiskQueryActor>,IQuery,CancellationToken,ValueTask>>
    {
        [typeof(GetRiskInvocationQuery)]=static(a,c,q,t)=>((GetRiskInvocationQuery)q).ExecuteAsync(a.services,c,t),
        [typeof(GetRiskResultQuery)]=static(a,c,q,t)=>((GetRiskResultQuery)q).ExecuteAsync(a.services,c,t),
        [typeof(GetRiskHistoryPageQuery)]=static(a,c,q,t)=>((GetRiskHistoryPageQuery)q).ExecuteAsync(a.services,c,t),
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,QueryExceptionHandler> _exceptionMap=CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<RiskQueryActor> c,IActorMessage m)=>ParseMappedQuery(c,m,_parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<RiskQueryActor> c,IQuery q)=>ReceiveAsync(c,q,CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<RiskQueryActor> c,IQuery q,CancellationToken t)=>ResolveMappedQueryHandler(q,_receiveMap)(this,c,q,t);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<RiskQueryActor> c,ActorThreadId id,IQuery q,string verb,Exception ex)=>ExceptionMappedQueryAsync(c,id,q,verb,ex,_exceptionMap);
    static IRiskQueryContext Typed(IQueryActorContext<RiskQueryActor> c)=>c as IRiskQueryContext??throw new ArgumentException("Typed Risk query context required.");
}
