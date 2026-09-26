using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
public interface IRiskQueryApi
{
    Task<ServiceResult<RiskObservation>> GetInvocationAsync(StrategyWorkflowId workflowId,Guid invocationId,CancellationToken cancellationToken=default);
    Task<ServiceResult<RiskAssessmentResult>> GetResultAsync(StrategyWorkflowId workflowId,Guid invocationId,Guid resultId,CancellationToken cancellationToken=default);
    Task<ServiceResult<RiskHistoryPage>> GetHistoryAsync(int portfolioId,int fundId,DateOnly valueDate,int pageSize=25,string? pagingState=null,CancellationToken cancellationToken=default);
}
