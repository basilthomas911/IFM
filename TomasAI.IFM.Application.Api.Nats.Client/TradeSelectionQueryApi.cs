using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.Api.Nats.Client;
public sealed class TradeSelectionQueryApi(IActorProducer producer):NatsClientApi(producer),ITradeSelectionQueryApi
{
    static SelectionQueryAccess Access(){var a=PortfolioAccessScope.Current??PortfolioAccessContext.Reader($"interactive:{Environment.UserName}");return new(a.Principal,[..a.Roles]);}
    static ActorSubject Subject(string verb,string key)=>new(ActorType.Query,GetTradeSelectionInvocationQuery.Actor,verb,key);
    public async Task<ServiceResult<TradeSelectionProjection>> GetInvocationAsync(StrategyWorkflowId workflowId,Guid invocationId,CancellationToken cancellationToken=default)
    {var q=new GetTradeSelectionInvocationQuery{Subject=Subject(GetTradeSelectionInvocationQuery.Verb,workflowId.ToString()),WorkflowId=workflowId,InvocationId=invocationId,Access=Access()};return await RequestAsync<GetTradeSelectionInvocationQuery,TradeSelectionProjection>(q.Subject,q,cancellationToken);}
    public async Task<ServiceResult<TradeSelectionResult>> GetResultAsync(StrategyWorkflowId workflowId,Guid invocationId,Guid resultId,CancellationToken cancellationToken=default)
    {var q=new GetTradeSelectionResultQuery{Subject=Subject(GetTradeSelectionResultQuery.Verb,workflowId.ToString()),WorkflowId=workflowId,InvocationId=invocationId,ResultId=resultId,Access=Access()};return await RequestAsync<GetTradeSelectionResultQuery,TradeSelectionResult>(q.Subject,q,cancellationToken);}
    public async Task<ServiceResult<TradeSelectionHistoryPage>> GetHistoryAsync(int portfolioId,int fundId,DateOnly valueDate,int pageSize=50,string? pagingState=null,CancellationToken cancellationToken=default)
    {var q=new GetTradeSelectionHistoryPageQuery{Subject=Subject(GetTradeSelectionHistoryPageQuery.Verb,$"{portfolioId}.{fundId}"),PortfolioId=portfolioId,FundId=fundId,ValueDate=valueDate,PageSize=pageSize,PagingState=pagingState,Access=Access()};return await RequestAsync<GetTradeSelectionHistoryPageQuery,TradeSelectionHistoryPage>(q.Subject,q,cancellationToken);}
}
