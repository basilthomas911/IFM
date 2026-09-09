using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.Api.Nats.Client;
public sealed class RiskQueryApi(IActorProducer producer):NatsClientApi(producer),IRiskQueryApi
{
    static CompositionQueryAccess Access(){var a=PortfolioAccessScope.Current??PortfolioAccessContext.Reader($"interactive:{Environment.UserName}");return new(a.Principal,[..a.Roles]);}
    static ActorSubject Subject(string verb,string key)=>new(ActorType.Query,GetRiskInvocationQuery.Actor,verb,key);
    public async Task<ServiceResult<RiskObservation>> GetInvocationAsync(StrategyWorkflowId workflowId,Guid invocationId,CancellationToken cancellationToken=default)
    {var q=new GetRiskInvocationQuery{Subject=Subject(GetRiskInvocationQuery.Verb,workflowId.ToString()),WorkflowId=workflowId,InvocationId=invocationId,Access=Access()};return await RequestAsync<GetRiskInvocationQuery,RiskObservation>(q.Subject,q,cancellationToken);}
    public async Task<ServiceResult<RiskAssessmentResult>> GetResultAsync(StrategyWorkflowId workflowId,Guid invocationId,Guid resultId,CancellationToken cancellationToken=default)
    {var q=new GetRiskResultQuery{Subject=Subject(GetRiskResultQuery.Verb,workflowId.ToString()),WorkflowId=workflowId,InvocationId=invocationId,ResultId=resultId,Access=Access()};return await RequestAsync<GetRiskResultQuery,RiskAssessmentResult>(q.Subject,q,cancellationToken);}
    public async Task<ServiceResult<RiskHistoryPage>> GetHistoryAsync(int portfolioId,int fundId,DateOnly valueDate,int pageSize=25,string? pagingState=null,CancellationToken cancellationToken=default)
    {var q=new GetRiskHistoryPageQuery{Subject=Subject(GetRiskHistoryPageQuery.Verb,$"{portfolioId}.{fundId}"),PortfolioId=portfolioId,FundId=fundId,ValueDate=valueDate,PageSize=pageSize,PagingState=pagingState,Access=Access()};return await RequestAsync<GetRiskHistoryPageQuery,RiskHistoryPage>(q.Subject,q,cancellationToken);}
}
