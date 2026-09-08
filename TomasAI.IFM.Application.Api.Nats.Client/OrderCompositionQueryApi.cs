using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.Api.Nats.Client;
public sealed class OrderCompositionQueryApi(IActorProducer producer):NatsClientApi(producer),IOrderCompositionQueryApi
{
    static CompositionQueryAccess Access(){var a=PortfolioAccessScope.Current??PortfolioAccessContext.Reader($"interactive:{Environment.UserName}");return new(a.Principal,[..a.Roles]);}
    static ActorSubject Subject(string verb,string key)=>new(ActorType.Query,GetOrderCompositionInvocationQuery.Actor,verb,key);
    public async Task<ServiceResult<OrderCompositionProjection>> GetInvocationAsync(StrategyWorkflowId workflowId,Guid invocationId,CancellationToken cancellationToken=default)
    {var q=new GetOrderCompositionInvocationQuery{Subject=Subject(GetOrderCompositionInvocationQuery.Verb,workflowId.ToString()),WorkflowId=workflowId,InvocationId=invocationId,Access=Access()};return await RequestAsync<GetOrderCompositionInvocationQuery,OrderCompositionProjection>(q.Subject,q,cancellationToken);}
    public async Task<ServiceResult<OrderCompositionResult>> GetResultAsync(StrategyWorkflowId workflowId,Guid invocationId,Guid resultId,CancellationToken cancellationToken=default)
    {var q=new GetOrderCompositionResultQuery{Subject=Subject(GetOrderCompositionResultQuery.Verb,workflowId.ToString()),WorkflowId=workflowId,InvocationId=invocationId,ResultId=resultId,Access=Access()};return await RequestAsync<GetOrderCompositionResultQuery,OrderCompositionResult>(q.Subject,q,cancellationToken);}
    public async Task<ServiceResult<OrderCompositionHistoryPage>> GetHistoryAsync(int portfolioId,int fundId,DateOnly valueDate,int pageSize=50,string? pagingState=null,CancellationToken cancellationToken=default)
    {var q=new GetOrderCompositionHistoryPageQuery{Subject=Subject(GetOrderCompositionHistoryPageQuery.Verb,$"{portfolioId}.{fundId}"),PortfolioId=portfolioId,FundId=fundId,ValueDate=valueDate,PageSize=pageSize,PagingState=pagingState,Access=Access()};return await RequestAsync<GetOrderCompositionHistoryPageQuery,OrderCompositionHistoryPage>(q.Subject,q,cancellationToken);}
}
