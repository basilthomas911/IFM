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
    static readonly IReadOnlyDictionary<Type,Func<TradeSelectionQueryActor,IQueryActorContext<TradeSelectionQueryActor>,IQuery,CancellationToken,ValueTask>> _receiveMap=new Dictionary<Type,Func<TradeSelectionQueryActor,IQueryActorContext<TradeSelectionQueryActor>,IQuery,CancellationToken,ValueTask>>
    {
        [typeof(GetTradeSelectionInvocationQuery)]=static async(a,c,q,t)=>{var x=(GetTradeSelectionInvocationQuery)q;var value=await a.Exact(x.Access,x.WorkflowId,x.InvocationId,t);await c.ReplyAsync(q.Subject.ThreadId,q.Subject.Verb,new ServiceOk<TradeSelectionProjection>(value));},
        [typeof(GetTradeSelectionResultQuery)]=static async(a,c,q,t)=>{var x=(GetTradeSelectionResultQuery)q;var value=await a.Exact(x.Access,x.WorkflowId,x.InvocationId,t);if(value.Completion.Result.ResultId!=x.ResultId)throw new KeyNotFoundException("Exact selector result not found.");await c.ReplyAsync(q.Subject.ThreadId,q.Subject.Verb,new ServiceOk<TradeSelectionResult>(TradeSelectionContracts.ReadResult(value.Completion.Result)));},
        [typeof(GetTradeSelectionHistoryPageQuery)]=static async(a,c,q,t)=>
        {
            var x=(GetTradeSelectionHistoryPageQuery)q;await a.Authorize(x.Access,x.PortfolioId,x.FundId,t);
            var page=await a.services.DbFactory.TradeDb.GetTradeSelectionHistoryAsync(x.PortfolioId,x.FundId,x.ValueDate,x.PageSize,TradeSelectionPaging.Decode(x.PortfolioId,x.FundId,x.ValueDate,x.PageSize,x.PagingState),t);
            await c.ReplyAsync(q.Subject.ThreadId,q.Subject.Verb,new ServiceOk<TradeSelectionHistoryPage>(new(page.Items,TradeSelectionPaging.Encode(x.PortfolioId,x.FundId,x.ValueDate,x.PageSize,page.PagingState))));
        }
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,QueryExceptionHandler> _exceptionMap=CreateQueryExceptionMap(_receiveMap.Keys);
    async Task Authorize(SelectionQueryAccess access,int portfolioId,int fundId,CancellationToken token)
    {
        if(access is null || string.IsNullOrWhiteSpace(access.Principal) || access.Roles.Length==0)throw new UnauthorizedAccessException("Portfolio read authority is required.");
        using var scope=PortfolioAccessScope.Push(new(){Principal=access.Principal,Roles=access.Roles});
        var result=await services.PortfolioQueries.GetFundAsync(portfolioId,fundId,cancellationToken:token);
        if(!result.Success || result.Value is null || result.Value.PortfolioId!=portfolioId || result.Value.FundId!=fundId)throw new UnauthorizedAccessException("Portfolio/Fund access was denied.");
    }
    async Task<TradeSelectionProjection> Exact(SelectionQueryAccess access,StrategyWorkflowId workflowId,Guid invocationId,CancellationToken token)
    {
        var completed=await services.DbFactory.TradeDb.GetTradeSelectionInvocationAsync(workflowId,invocationId,token)??throw new KeyNotFoundException("Exact selector invocation not found.");
        var result=TradeSelectionContracts.ReadResult(completed.Result);await Authorize(access,result.PortfolioId,result.FundId,token);
        var workflow=await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowAsync(workflowId,token);
        var view=workflow is null?null:MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>(workflow.StatePayload);
        var accepted=view?.TradeSelection.Result?.PayloadSha256==completed.Result.PayloadSha256 && view.TradeSelection.SourceEventId==completed.Id;
        return new(completed,accepted,view is null,!accepted && view is {Status:not WorkflowStrategyMachineStatus.Started});
    }
    protected override IQuery ParseMessage(IQueryActorContext<TradeSelectionQueryActor> c,IActorMessage m)=>ParseMappedQuery(c,m,_parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<TradeSelectionQueryActor> c,IQuery q)=>ReceiveAsync(c,q,CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<TradeSelectionQueryActor> c,IQuery q,CancellationToken t)=>ResolveMappedQueryHandler(q,_receiveMap)(this,c,q,t);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<TradeSelectionQueryActor> c,ActorThreadId id,IQuery q,string verb,Exception ex)=>ExceptionMappedQueryAsync(c,id,q,verb,ex,_exceptionMap);
    static ITradeSelectionQueryContext Typed(IQueryActorContext<TradeSelectionQueryActor> c)=>c as ITradeSelectionQueryContext??throw new ArgumentException("Typed selector query context required.");
}
