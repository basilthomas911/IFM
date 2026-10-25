using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
public interface ITradeSelectionQueryApi
{
    Task<ServiceResult<TradeSelectionProjection>> GetInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken cancellationToken = default);
    Task<ServiceResult<TradeSelectionResult>> GetResultAsync(StrategyWorkflowId workflowId, Guid invocationId, Guid resultId, CancellationToken cancellationToken = default);
    Task<ServiceResult<TradeSelectionHistoryPage>> GetHistoryAsync(int portfolioId, int fundId, DateOnly valueDate, int pageSize = 50, string? pagingState = null, CancellationToken cancellationToken = default);
}
